using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;
using Admin = RunstarSystems.SystemAdmin;

namespace RunstarSystems.IKSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Builders.IkChainBuilder))]
    public class IkDualEndJobRunner : MonoBehaviour
    {
        [Header("System Linking")]
        // Get the data from the ik chain builder
        public Builders.IkChainBuilder ik_builder;

        [Header("Targets")]
        [Tooltip("For each subchain, assign a target")]
        public List<Transform> tip_targets = new List<Transform>();

        [Tooltip("For each subchain, assign its root.")]
        public List<Transform> root_targets = new List<Transform>();

        [Header("Pull Target")]
        [Tooltip("Forces a particular bend in the arm. Optional")]
        public Transform pull_target;
        public bool pull_enabled = true;

        [Header("Solver")]
        public SolveMode solve_mode = SolveMode.Alternate;
        // Amount of FABRIK passes before it gives up for that frame
        public int iterations = 10;
        // How close the end effector can be to end early
        public float tolerance = 0.001f;
        // How much does this ik take over (blend) animations
        public float relaxation = 0.6f;
        // Z rotations only 
        // more work but could eventually change this to any axis
        public bool planar_2d = true;
        // Joint constraints (2d or 3d)
        public bool constraints_enabled = true;

        [Header("Pull")]
        // How much does the middle joint rotate to pull
        public float pull_strength = 0.35f;
        // How much the arm is willing to give to that pull
        public float pull_bias = 0.5f;
        // How many times with in the FABRIK loop does that pass happen
        public int pull_passes = 1;

        [Header("Stability")]
        // lerp for end effector and target
        public float target_smoothing = 0f;
        public float global_damping = 0.85f;

        // The pass order changes depending on if root moves or target or both
        public bool adaptive_pass_order = true;
        public bool break_two_cycle = true;
        // Attempts to stop jitters from small changes
        public float two_cycle_eps2 = 1e-10f;
        // Attempts to stop jitters from unstable locations
        public float two_cycle_mix = 0.5f;

        [Header("Output")]
        public bool rotation_only = true;
        public bool unscale_during_rotation = true;
        // Gets the system from what is registered
        private IKSystem mySystem;
        // Gives a list of the entire subchain
        private List<int> globalChainIndices = new List<int>();

        private void OnEnable()
        {
            if (ik_builder == null)
            {
                ik_builder = GetComponent<Builders.IkChainBuilder>();
            }

            if (ik_builder == null)
            {
                return;
            }

            if (ik_builder.subChains == null || ik_builder.subChains.Count == 0)
            {
                ik_builder.Build();
            }

            // Systems act like querable singletons, 
            // I might regret later
            mySystem = Admin.SystemRegistry.GetRegisteredSystems().OfType<IKSystem>().FirstOrDefault();

            if (mySystem == null)
            {
                return;
            }

            // Register this ik chain to the ik system
            mySystem.RegisterRunner(
                this,
                ik_builder.subChains,
                ik_builder.uniqueTransforms,
                ik_builder.subChainTransformMaps,
                globalChainIndices);
        }

        private void OnDisable()
        {
            if (mySystem == null)
            {
                return;
            }

            mySystem.UnregisterRunner(this);
            globalChainIndices.Clear();
        }

        public void UpdateJobData()
        {
            // Make sure everything exsists
            if (!CanUpdateJobData())
            {
                return;
            }

            // Iterate through all subchains
            int chain_count = Mathf.Min(
                ik_builder.subChains.Count,
                globalChainIndices.Count);

            for (int chain_index = 0; chain_index < chain_count; chain_index++)
            {
                int global_chain_index = globalChainIndices[chain_index];

                // Make sure subchain still exsists
                if (!IsValidGlobalChainIndex(global_chain_index))
                {
                    continue;
                }

                // Make sure root still exsists
                if (!TryGetRootTransform(chain_index, out Transform root_transform))
                {
                    continue;
                }

                // Find end effector
                Transform tip_transform = ResolveTipTarget(chain_index);
                // Set the root for this sunchain
                Transform root_target_transform = ResolveRootTarget(chain_index, root_transform);
                // Get the roots parent for solve space 
                // maybe add settable spaces here later
                Transform solve_space = ResolveSolveSpace(root_transform);

                float4x4 world_to_solve = solve_space != null
                    ? solve_space.worldToLocalMatrix
                    : float4x4.identity;

                float4x4 solve_to_world = solve_space != null
                    ? solve_space.localToWorldMatrix
                    : float4x4.identity;

                // World position of root
                float3 start_world = root_target_transform.position;
                
                // World position of end effector
                float3 end_world = tip_transform.position;
                
                // World position of the pull
                float3 pull_world = ResolvePullWorldPosition(end_world);

                float3 start_solve = math.transform(world_to_solve, start_world);
                float3 end_solve = math.transform(world_to_solve, end_world);
                float3 pull_solve = math.transform(world_to_solve, pull_world);

                SyncChainState(
                    chain_index,
                    global_chain_index,
                    start_solve,
                    end_solve);

                SyncSolverSettings(
                    global_chain_index,
                    world_to_solve,
                    solve_to_world,
                    pull_solve);
            }
        }

        private bool CanUpdateJobData()
        {
            if (ik_builder == null)
            {
                return false;
            }

            if (ik_builder.subChains == null)
            {
                return false;
            }

            if (ik_builder.subChainTransformMaps == null)
            {
                return false;
            }

            if (ik_builder.uniqueTransforms == null)
            {
                return false;
            }

            if (mySystem == null)
            {
                return false;
            }

            if (!mySystem.GlobalChains.IsCreated)
            {
                return false;
            }

            if (!mySystem.GlobalSettings.IsCreated)
            {
                return false;
            }

            return true;
        }

        private bool IsValidGlobalChainIndex(int global_chain_index)
        {
            if (global_chain_index < 0)
            {
                return false;
            }

            if (global_chain_index >= mySystem.GlobalChains.Length)
            {
                return false;
            }

            return true;
        }

        private bool TryGetRootTransform(int chain_index, out Transform root_transform)
        {
            root_transform = null;

            if (chain_index >= ik_builder.subChainTransformMaps.Count)
            {
                return false;
            }

            if (ik_builder.subChainTransformMaps[chain_index] == null)
            {
                return false;
            }

            if (ik_builder.subChainTransformMaps[chain_index].transformIndices == null)
            {
                return false;
            }

            if (ik_builder.subChainTransformMaps[chain_index].transformIndices.Count == 0)
            {
                return false;
            }

            int root_transform_index = ik_builder.subChainTransformMaps[chain_index].transformIndices[0];

            if (root_transform_index < 0)
            {
                return false;
            }

            if (root_transform_index >= ik_builder.uniqueTransforms.Count)
            {
                return false;
            }

            root_transform = ik_builder.uniqueTransforms[root_transform_index];

            if (root_transform == null)
            {
                return false;
            }

            return true;
        }

        private Transform ResolveTipTarget(int chain_index)
        {
            if (chain_index < tip_targets.Count)
            {
                if (tip_targets[chain_index] != null)
                {
                    return tip_targets[chain_index];
                }
            }

            // I really should just make this fail because every end effector
            // should have a target and making a default is buggy and harder to
            // detect.
            return transform;
        }

        private Transform ResolveRootTarget(int chain_index, Transform root_transform)
        {
            if (chain_index < root_targets.Count)
            {
                if (root_targets[chain_index] != null)
                {
                    return root_targets[chain_index];
                }
            }

            return root_transform;
        }

        private Transform ResolveSolveSpace(Transform root_transform)
        {
            // Add a solve space override but make it individual
            return root_transform.parent;
        }

        private float3 ResolvePullWorldPosition(float3 fallback_position)
        {
            if (!pull_enabled)
            {
                return fallback_position;
            }

            if (pull_target == null)
            {
                return fallback_position;
            }

            return pull_target.position;
        }

        private void SyncChainState(
            int chain_index,
            int global_chain_index,
            float3 start_world,
            float3 end_world)
        {
            IKChainState global_state = mySystem.GlobalChains[global_chain_index];

            global_state.StartTargetPos = start_world;
            global_state.EndTargetPos = end_world;
            global_state.MaxIterations = Mathf.Max(1, iterations);
            global_state.Tolerance = Mathf.Max(0f, tolerance);
            global_state.SolveWeight = 1f;
            global_state.ConstraintsEnabled = constraints_enabled;

            mySystem.GlobalChains[global_chain_index] = global_state;

            IkSubChainData sub_chain = ik_builder.subChains[chain_index];
            IKChainState source_state = sub_chain.chainState;

            source_state.StartTargetPos = start_world;
            source_state.EndTargetPos = end_world;
            source_state.MaxIterations = Mathf.Max(1, iterations);
            source_state.Tolerance = Mathf.Max(0f, tolerance);
            source_state.SolveWeight = 1f;
            source_state.ConstraintsEnabled = constraints_enabled;

            sub_chain.chainState = source_state;
            ik_builder.subChains[chain_index] = sub_chain;
        }

        private void SyncSolverSettings(
            int global_chain_index,
            float4x4 world_to_solve,
            float4x4 solve_to_world,
            float3 pull_solve)
        {
            if (global_chain_index >= mySystem.GlobalSettings.Length)
            {
                return;
            }

            DualEndSolverSettings settings = mySystem.GlobalSettings[global_chain_index];

            settings.WorldToSolveSpace = world_to_solve;
            settings.SolveSpaceToWorld = solve_to_world;
            settings.PullEndLocal = pull_solve;

            settings.SolveMode = solve_mode;
            settings.Iterations = Mathf.Max(1, iterations);
            settings.Tolerance = Mathf.Max(0f, tolerance);
            settings.Relaxation = math.clamp(relaxation, 0f, 1f);
            settings.Planar2D = planar_2d;
            settings.AdaptivePassOrder = adaptive_pass_order;
            settings.TargetSmoothing = math.max(0f, target_smoothing);

            if (pull_enabled)
            {
                settings.PullStrength = math.clamp(pull_strength, 0f, 1f);
                settings.PullBias = math.clamp(pull_bias, 0f, 1f);
                settings.PullPasses = Mathf.Max(0, pull_passes);
            }
            else
            {
                settings.PullStrength = 0f;
                settings.PullBias = math.clamp(pull_bias, 0f, 1f);
                settings.PullPasses = 0;
            }

            settings.GlobalDamping = math.clamp(global_damping, 0f, 1f);
            settings.BreakTwoCycle = break_two_cycle;
            settings.TwoCycleEps2 = math.max(0f, two_cycle_eps2);
            settings.TwoCycleMix = math.clamp(two_cycle_mix, 0f, 1f);

            mySystem.GlobalSettings[global_chain_index] = settings;
        }
    }
}
