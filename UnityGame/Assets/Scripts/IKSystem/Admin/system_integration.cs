using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;
using RunstarSystems.IKSystem.Solver;
using RunstarSystems.IKSystem.Builders;
using Admin = RunstarSystems.SystemAdmin;

namespace RunstarSystems.IKSystem
{
    public class RunnerSyncData
    {
        public IkDualEndJobRunner Runner;
        public List<IkSubChainData> SourceSubChains;
        public List<int> GlobalIndices;
        public List<Transform> UniqueTransforms;
        public List<SubChainTransformMap> TransformMaps;
        public int UniqueTransformOffset;
    }

    public class IKSystem : Admin.IGameSystem, IDisposable
    {
        public int ExecutionPriority => 50;

        private readonly List<RunnerSyncData> activeRunners = new List<RunnerSyncData>();

        // Native list hell (I want an ecs)
        // @Reminder Change this in a more complex game to an ecs
        public NativeList<IKChainState> GlobalChains;
        public NativeList<DualEndSolverSettings> GlobalSettings;
        public NativeList<DualEndSolverState> GlobalStates;

        public NativeList<JointState> GlobalJoints;
        public NativeList<BoneStaticData> GlobalBoneStatics;
        public NativeList<JointStaticData> GlobalJointStatics;
        public NativeList<int> GlobalTransformIndices;

        public NativeList<float3> WorkingSolvePositions;
        public NativeList<float3> WorkingPreviousSolvePositions;
        public NativeList<float3> WorkingOlderSolvePositions;

        public NativeList<float3> OutputWorldPositions;
        public NativeList<quaternion> OutputLocalRotations;

        public NativeList<quaternion> CurrentWorldRotations;

        public NativeList<int> SpineIndices;
        public NativeList<int> LimbIndices;
        public NativeList<int> TailIndices;

        public NativeList<float3> WorkingBestSolvePositions;
        public NativeList<float3> WorkingTempSolvePositions;

        // A switch for what to debug
        // @TODO Create a debug system or find one that unity has
        public bool debugEnabled = false;
        public bool debugEveryFrame = false;
        public bool debugTargetSync = false;
        public bool debugTransformSync = false;
        public bool debugPostSync = false;
        public bool debugRegistration = false;

        // Debug message rate
        private int debugFrameInterval = 30;
        private int tickCount;
        private bool isDisposed;
        
        // Allows this singleton system to register to the admin before anything else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Subscribe()
        {
            Admin.SystemRegistry.Register(new IKSystem());
        }

        // Allows to share data between the burst compiled data and unity.
        // Better replaced as an ecs for later
        public void Setup()
        {
            GlobalChains = new NativeList<IKChainState>(Allocator.Persistent);
            GlobalSettings = new NativeList<DualEndSolverSettings>(Allocator.Persistent);
            GlobalStates = new NativeList<DualEndSolverState>(Allocator.Persistent);

            GlobalJoints = new NativeList<JointState>(Allocator.Persistent);
            GlobalBoneStatics = new NativeList<BoneStaticData>(Allocator.Persistent);
            GlobalJointStatics = new NativeList<JointStaticData>(Allocator.Persistent);
            GlobalTransformIndices = new NativeList<int>(Allocator.Persistent);

            WorkingSolvePositions = new NativeList<float3>(Allocator.Persistent);
            WorkingPreviousSolvePositions = new NativeList<float3>(Allocator.Persistent);
            WorkingOlderSolvePositions = new NativeList<float3>(Allocator.Persistent);

            OutputWorldPositions = new NativeList<float3>(Allocator.Persistent);
            OutputLocalRotations = new NativeList<quaternion>(Allocator.Persistent);
            CurrentWorldRotations = new NativeList<quaternion>(Allocator.Persistent);

            SpineIndices = new NativeList<int>(Allocator.Persistent);
            LimbIndices = new NativeList<int>(Allocator.Persistent);
            TailIndices = new NativeList<int>(Allocator.Persistent);

            WorkingBestSolvePositions = new NativeList<float3>(Allocator.Persistent);
            WorkingTempSolvePositions = new NativeList<float3>(Allocator.Persistent);
            
            Application.quitting += Dispose;

            DebugSystem("IK setup complete.");
        }

        // This tick system is called by the admin in a loop
        // Called at Execution priority 50. So late into lateUpdate
        // generally last called
        public void Tick(float dt)
        {
            tickCount++;

            // Check to make sure chains are not empty
            if (GlobalChains.Length == 0)
            {
                DebugSystemThrottled("Tick skipped because there are no global chains.");
                return;
            }

            // Keeps track of the dualEndJobRunner
            if (activeRunners.Count == 0)
            {
                DebugSystemThrottled("Tick skipped because there are no active runners.");
                return;
            }

            DebugSystemThrottled(
                "Tick started:\n\tChains " + GlobalChains.Length +
                " \n\tTransforms " + OutputWorldPositions.Length +
                " \n\tRunners " + activeRunners.Count);

            SyncTransformsToNative();

            DebugFirstChainBeforeJob();

            BurstDualEndSolverJob base_job = new BurstDualEndSolverJob
            {
                DeltaTime = dt,
                ChainStates = GlobalChains.AsArray(),
                Settings = GlobalSettings.AsArray(),
                JointStates = GlobalJoints.AsArray(),
                JointStatics = GlobalJointStatics.AsArray(),
                BoneStatics = GlobalBoneStatics.AsArray(),
                TransformIndices = GlobalTransformIndices.AsArray(),
                SolvePositions = WorkingSolvePositions.AsArray(),
                PreviousSolvePositions = WorkingPreviousSolvePositions.AsArray(),
                BestSolvePositions = WorkingBestSolvePositions.AsArray(),
                TempSolvePositions = WorkingTempSolvePositions.AsArray(),
                OutputWorldPositions = OutputWorldPositions.AsArray(),
                OutputLocalRotations = OutputLocalRotations.AsArray(),
                OutputWorldRotations = CurrentWorldRotations.AsArray(),
            };

            JobHandle dependency = new JobHandle();

            if (SpineIndices.Length > 0)
            {
                base_job.PhaseIndices = SpineIndices.AsArray();
                dependency = base_job.Schedule(SpineIndices.Length, 1, dependency);
            }

            if (LimbIndices.Length > 0)
            {
                base_job.PhaseIndices = LimbIndices.AsArray();
                dependency = base_job.Schedule(LimbIndices.Length, 1, dependency);
            }

            if (TailIndices.Length > 0)
            {
                base_job.PhaseIndices = TailIndices.AsArray();
                dependency = base_job.Schedule(TailIndices.Length, 1, dependency);
            }

            dependency.Complete();

            DebugFirstChainAfterJob();

            SyncNativeToTransforms();

            DebugSystemThrottled("Tick finished.");
        }

        /*
         * Copies dynamic runner state and current Transform state into native buffers.
         */
        private void SyncTransformsToNative()
        {
            for (int runner_index = 0; runner_index < activeRunners.Count; runner_index++)
            {
                RunnerSyncData sync_data = activeRunners[runner_index];

                if (sync_data == null)
                {
                    DebugWarning("Sync skipped because runner data is null.");
                    continue;
                }

                if (sync_data.Runner == null)
                {
                    DebugWarning("Sync skipped because runner is null.");
                    continue;
                }

                sync_data.Runner.UpdateJobData();
                SyncRunnerChainStatesToNative(sync_data);

                int offset = sync_data.UniqueTransformOffset;

                for (int transform_index = 0; transform_index < sync_data.UniqueTransforms.Count; transform_index++)
                {
                    Transform source_transform = sync_data.UniqueTransforms[transform_index];

                    if (source_transform == null)
                    {
                        DebugWarning("Null transform found during pre sync.");
                        continue;
                    }

                    int global_transform_index = offset + transform_index;

                    if (global_transform_index < 0)
                    {
                        DebugWarning("Negative global transform index during pre sync.");
                        continue;
                    }

                    if (global_transform_index >= OutputWorldPositions.Length)
                    {
                        DebugWarning("Global transform index outside position buffer during pre sync.");
                        continue;
                    }

                    if (global_transform_index >= OutputLocalRotations.Length)
                    {
                        DebugWarning("Global transform index outside local rotation buffer during pre sync.");
                        continue;
                    }

                    if (global_transform_index >= CurrentWorldRotations.Length)
                    {
                        DebugWarning("Global transform index outside world rotation buffer during pre sync.");
                        continue;
                    }

                    OutputWorldPositions[global_transform_index] = source_transform.position;
                    OutputLocalRotations[global_transform_index] = source_transform.localRotation;
                    CurrentWorldRotations[global_transform_index] = source_transform.rotation;

                    if (debugTransformSync && ShouldDebugThisFrame() && transform_index < 4)
                    {
                        DebugSystem(
                            "Pre sync transform " + transform_index +
                            " global " + global_transform_index +
                            " name " + source_transform.name +
                            " world " + source_transform.position +
                            " local rot z " + source_transform.localEulerAngles.z);
                    }
                }
            }
        }

        /*
        * Copies dynamic chain state from the runner owned source data into native global chains.
        *
        * @param sync_data Runner data to sync.
        */
        private void SyncRunnerChainStatesToNative(RunnerSyncData sync_data)
        {
            if (sync_data.SourceSubChains == null)
            {
                DebugWarning("Source sub chains are null.");
                return;
            }

            for (int chain_index = 0; chain_index < sync_data.GlobalIndices.Count; chain_index++)
            {
                if (chain_index >= sync_data.SourceSubChains.Count)
                {
                    DebugWarning("Chain index outside source sub chains during target sync.");
                    continue;
                }

                int global_chain_index = sync_data.GlobalIndices[chain_index];

                if (global_chain_index < 0)
                {
                    continue;
                }

                if (global_chain_index >= GlobalChains.Length)
                {
                    DebugWarning("Global chain index outside global chains during target sync.");
                    continue;
                }

                IKChainState previous_state = GlobalChains[global_chain_index];
                IKChainState source_state = sync_data.SourceSubChains[chain_index].chainState;

                float3 previous_target = previous_state.EndTargetPos;
                float3 source_target = source_state.EndTargetPos;

                IKChainState updated_state = source_state;

                updated_state.ElementStartIndex = previous_state.ElementStartIndex;
                updated_state.ElementCount = previous_state.ElementCount;

                GlobalChains[global_chain_index] = updated_state;

                float target_delta = math.distance(previous_target, source_target);

                if (debugTargetSync && ShouldDebugThisFrame())
                {
                    DebugSystem(
                        "Target sync chain " + chain_index +
                        " global " + global_chain_index +
                        " previous target " + previous_target +
                        " source target " + source_target +
                        " delta " + target_delta +
                        " start target " + source_state.StartTargetPos +
                        " solve weight " + source_state.SolveWeight +
                        " constraints " + source_state.ConstraintsEnabled +
                        " element start " + updated_state.ElementStartIndex +
                        " element count " + updated_state.ElementCount);
                }

                if (debugTargetSync && debugEveryFrame)
                {
                    DebugSystem(
                        "Target live check chain " + chain_index +
                        " source EndTargetPos " + source_target);
                }
            }
        }

        /*
         * Copies solved native state back to the correct Unity Transforms.
         */
        private void SyncNativeToTransforms()
        {
            for (int runner_index = 0; runner_index < activeRunners.Count; runner_index++)
            {
                RunnerSyncData sync_data = activeRunners[runner_index];

                if (sync_data == null)
                {
                    DebugWarning("Post sync skipped because runner data is null.");
                    continue;
                }

                if (sync_data.Runner == null)
                {
                    DebugWarning("Post sync skipped because runner is null.");
                    continue;
                }

                IkDualEndJobRunner runner = sync_data.Runner;

                for (int chain_index = 0; chain_index < sync_data.GlobalIndices.Count; chain_index++)
                {
                    if (chain_index >= sync_data.TransformMaps.Count)
                    {
                        DebugWarning("Post sync missing transform map.");
                        continue;
                    }

                    int global_chain_index = sync_data.GlobalIndices[chain_index];

                    if (global_chain_index < 0)
                    {
                        continue;
                    }

                    if (global_chain_index >= GlobalChains.Length)
                    {
                        DebugWarning("Post sync global chain index outside range.");
                        continue;
                    }

                    IKChainState state = GlobalChains[global_chain_index];

                    int element_start_index = state.ElementStartIndex;
                    int element_count = state.ElementCount;

                    SubChainTransformMap transform_map = sync_data.TransformMaps[chain_index];

                    if (transform_map == null)
                    {
                        DebugWarning("Post sync transform map is null.");
                        continue;
                    }

                    if (transform_map.transformIndices == null)
                    {
                        DebugWarning("Post sync transform index list is null.");
                        continue;
                    }

                    List<int> local_transform_indices = transform_map.transformIndices;

                    if (local_transform_indices.Count != element_count)
                    {
                        DebugWarning(
                            "Post sync map count does not match element count. Map " +
                            local_transform_indices.Count + " elements " + element_count);
                    }

                    for (int element_index = 0; element_index < element_count; element_index++)
                    {
                        if (element_index >= local_transform_indices.Count)
                        {
                            DebugWarning("Post sync element index outside local transform map.");
                            continue;
                        }

                        int local_unique_index = local_transform_indices[element_index];

                        if (local_unique_index < 0)
                        {
                            DebugWarning("Post sync local unique index is negative.");
                            continue;
                        }

                        if (local_unique_index >= sync_data.UniqueTransforms.Count)
                        {
                            DebugWarning("Post sync local unique index outside unique transforms.");
                            continue;
                        }

                        Transform target_transform = sync_data.UniqueTransforms[local_unique_index];

                        if (target_transform == null)
                        {
                            DebugWarning("Post sync target transform is null.");
                            continue;
                        }

                        int global_element_index = element_start_index + element_index;

                        if (global_element_index < 0)
                        {
                            DebugWarning("Post sync global element index is negative.");
                            continue;
                        }

                        if (global_element_index >= GlobalTransformIndices.Length)
                        {
                            DebugWarning("Post sync global element index outside transform index buffer.");
                            continue;
                        }

                        int global_transform_index = GlobalTransformIndices[global_element_index];

                        if (global_transform_index < 0)
                        {
                            DebugWarning("Post sync global transform index is negative.");
                            continue;
                        }

                        if (global_transform_index >= OutputLocalRotations.Length)
                        {
                            DebugWarning("Post sync global transform index outside rotation buffer.");
                            continue;
                        }

                        Vector3 previous_position = target_transform.position;
                        Quaternion previous_rotation = target_transform.localRotation;

                        if (!runner.rotation_only)
                        {
                            if (global_element_index < WorkingSolvePositions.Length)
                            {
                                target_transform.position = WorkingSolvePositions[global_element_index];
                            }
                        }

                        quaternion target_local_rotation = OutputLocalRotations[global_transform_index];

                        if (state.SolveWeight >= 0.999f)
                        {
                            target_transform.localRotation = target_local_rotation;
                        }
                        else
                        {
                            target_transform.localRotation = math.slerp(
                                target_transform.localRotation,
                                target_local_rotation,
                                state.SolveWeight);
                        }

                        if (debugPostSync && ShouldDebugThisFrame() && element_index < 6)
                        {
                            float position_delta = Vector3.Distance(previous_position, target_transform.position);
                            float rotation_delta = Quaternion.Angle(previous_rotation, target_transform.localRotation);

                            DebugSystem(
                                "Post sync chain " + chain_index +
                                " element " + element_index +
                                " name " + target_transform.name +
                                " local unique " + local_unique_index +
                                " global element " + global_element_index +
                                " global transform " + global_transform_index +
                                " pos delta " + position_delta +
                                " rot delta " + rotation_delta +
                                " solved pos " + GetDebugPosition(global_element_index) +
                                " solved rot z " + ((Quaternion)target_local_rotation).eulerAngles.z);
                        }
                    }
                }
            }
        }

        /*
         * Registers one IK runner and bakes its local transform maps into global native indices.
         *
         * @param runner Runner component that owns the chains.
         * @param subChains Baked sub chain data from the builder.
         * @param uniqueTransforms Deduplicated transform list from the builder.
         * @param maps Per chain element to unique transform maps.
         * @param outGlobalIndices Output global chain indices owned by this runner.
         */
        public void RegisterRunner(
            IkDualEndJobRunner runner,
            List<IkSubChainData> subChains,
            List<Transform> uniqueTransforms,
            List<SubChainTransformMap> maps,
            List<int> outGlobalIndices)
        {
            if (runner == null)
            {
                DebugWarning("Register failed because runner is null.");
                return;
            }

            if (subChains == null)
            {
                DebugWarning("Register failed because sub chains are null.");
                return;
            }

            if (uniqueTransforms == null)
            {
                DebugWarning("Register failed because unique transforms are null.");
                return;
            }

            if (maps == null)
            {
                DebugWarning("Register failed because maps are null.");
                return;
            }

            if (outGlobalIndices == null)
            {
                DebugWarning("Register failed because output global indices are null.");
                return;
            }

            for (int runner_index = 0; runner_index < activeRunners.Count; runner_index++)
            {
                RunnerSyncData existing_data = activeRunners[runner_index];

                if (existing_data == null)
                {
                    continue;
                }

                if (existing_data.Runner != runner)
                {
                    continue;
                }

                DebugSystem("Register skipped because runner is already active.");

                if (!ReferenceEquals(existing_data.GlobalIndices, outGlobalIndices))
                {
                    outGlobalIndices.Clear();

                    for (int index = 0; index < existing_data.GlobalIndices.Count; index++)
                    {
                        outGlobalIndices.Add(existing_data.GlobalIndices[index]);
                    }
                }

                return;
            }

            outGlobalIndices.Clear();

            int unique_transform_offset = OutputWorldPositions.Length;

            RunnerSyncData sync_data = new RunnerSyncData
            {
                Runner = runner,
                SourceSubChains = subChains,
                UniqueTransforms = uniqueTransforms,
                TransformMaps = maps,
                GlobalIndices = outGlobalIndices,
                UniqueTransformOffset = unique_transform_offset
            };

            activeRunners.Add(sync_data);

            if (debugRegistration)
            {
                DebugSystem(
                    "Register runner. Chains " + subChains.Count +
                    " unique transforms " + uniqueTransforms.Count +
                    " offset " + unique_transform_offset);
            }

            RegisterUniqueTransformBuffers(uniqueTransforms, unique_transform_offset);

            for (int chain_index = 0; chain_index < subChains.Count; chain_index++)
            {
                RegisterChain(chain_index, subChains, maps, uniqueTransforms, unique_transform_offset, outGlobalIndices);
            }

            DebugSystem("Register complete. Global chains " + GlobalChains.Length);
        }

        /*
         * Registers all unique transform snapshots for one runner.
         *
         * @param uniqueTransforms Deduplicated transform list from the builder.
         * @param unique_transform_offset Offset into global transform buffers.
         */
        private void RegisterUniqueTransformBuffers(List<Transform> uniqueTransforms, int unique_transform_offset)
        {
            for (int transform_index = 0; transform_index < uniqueTransforms.Count; transform_index++)
            {
                Transform source_transform = uniqueTransforms[transform_index];

                if (source_transform == null)
                {
                    OutputWorldPositions.Add(float3.zero);
                    OutputLocalRotations.Add(quaternion.identity);
                    CurrentWorldRotations.Add(quaternion.identity);
                    DebugWarning("Register found null unique transform.");
                    continue;
                }

                OutputWorldPositions.Add(source_transform.position);
                OutputLocalRotations.Add(source_transform.localRotation);
                CurrentWorldRotations.Add(source_transform.rotation);

                if (debugRegistration && transform_index < 8)
                {
                    DebugSystem(
                        "Register transform local " + transform_index +
                        " global " + (unique_transform_offset + transform_index) +
                        " name " + source_transform.name +
                        " position " + source_transform.position +
                        " local rot z " + source_transform.localEulerAngles.z);
                }
            }
        }

        /*
        * Gets the mapped joint static data for one element.
        *
        * @param element_index Chain element index.
        * @param joint_state_index Source joint state index or negative.
        * @param sub_chain Source sub chain.
        */
        private JointStaticData GetMappedJointStaticData(
            int element_index,
            int joint_state_index,
            IkSubChainData sub_chain)
        {
            if (joint_state_index < 0)
            {
                return default(JointStaticData);
            }

            if (joint_state_index >= sub_chain.jointStatics.Count)
            {
                DebugWarning("Joint static index outside source list at element " + element_index + ".");
                return default(JointStaticData);
            }

            return sub_chain.jointStatics[joint_state_index];
        }

        /*
         * Registers one chain into the global native buffers.
         *
         * @param chain_index Source chain index.
         * @param subChains Source sub chain list.
         * @param maps Source transform map list.
         * @param uniqueTransforms Source unique transform list.
         * @param unique_transform_offset Global transform offset.
         * @param outGlobalIndices Output global chain indices.
         */
        private void RegisterChain(
            int chain_index,
            List<IkSubChainData> subChains,
            List<SubChainTransformMap> maps,
            List<Transform> uniqueTransforms,
            int unique_transform_offset,
            List<int> outGlobalIndices)
        {
            if (chain_index >= maps.Count)
            {
                outGlobalIndices.Add(-1);
                DebugWarning("Register missing transform map for chain.");
                return;
            }

            IkSubChainData sub_chain = subChains[chain_index];
            SubChainTransformMap transform_map = maps[chain_index];

            if (!ValidateRegistrationInputs(chain_index, sub_chain, transform_map))
            {
                outGlobalIndices.Add(-1);
                return;
            }

            List<int> local_transform_indices = transform_map.transformIndices;
            List<int> joint_state_indices = transform_map.jointStateIndices;
            List<int> bone_static_indices = transform_map.boneStaticIndices;

            int element_count = local_transform_indices.Count;

            int global_chain_index = GlobalChains.Length;
            int element_start_index = GlobalJoints.Length;

            outGlobalIndices.Add(global_chain_index);

            if (debugRegistration)
            {
                DebugSystem(
                    "Register chain " + chain_index +
                    " global " + global_chain_index +
                    " start " + element_start_index +
                    " element count " + element_count +
                    " jointStates " + sub_chain.jointStates.Count +
                    " boneStatics " + sub_chain.boneStatics.Count);
            }

            for (int element_index = 0; element_index < element_count; element_index++)
            {
                int local_unique_index = local_transform_indices[element_index];
                int global_transform_index = RegisterElementTransformIndex(
                    element_index,
                    local_unique_index,
                    unique_transform_offset,
                    uniqueTransforms.Count);

                JointState joint_state = GetMappedJointState(
                    element_index,
                    joint_state_indices[element_index],
                    sub_chain);

                JointStaticData joint_static_data = GetMappedJointStaticData(
                    element_index,
                    joint_state_indices[element_index],
                    sub_chain);

                GlobalJoints.Add(joint_state);
                GlobalJointStatics.Add(joint_static_data);

                BoneStaticData bone_static_data = GetMappedBoneStaticData(
                    element_index,
                    bone_static_indices[element_index],
                    sub_chain,
                    transform_map,
                    uniqueTransforms);

                GlobalJoints.Add(joint_state);
                GlobalBoneStatics.Add(bone_static_data);

                WorkingSolvePositions.Add(float3.zero);
                WorkingPreviousSolvePositions.Add(float3.zero);
                WorkingOlderSolvePositions.Add(float3.zero);
                WorkingBestSolvePositions.Add(float3.zero);
                WorkingTempSolvePositions.Add(float3.zero);

                if (debugRegistration && element_index < 12)
                {
                    DebugSystem(
                        "Register element " + element_index +
                        " local unique " + local_unique_index +
                        " global transform " + global_transform_index +
                        " joint map " + joint_state_indices[element_index] +
                        " bone map " + bone_static_indices[element_index] +
                        " rest length " + bone_static_data.RestLength);
                }
            }

            IKChainState state = sub_chain.chainState;
            state.ElementStartIndex = element_start_index;
            state.ElementCount = element_count;
            state.SolveWeight = 1f;

            GlobalChains.Add(state);

            DualEndSolverSettings solver_settings = new DualEndSolverSettings();
            solver_settings.Planar2D = true;
            solver_settings.Relaxation = 0.6f;
            solver_settings.Iterations = 12;
            solver_settings.Tolerance = 0.001f;
            solver_settings.WorldToSolveSpace = float4x4.identity;

            GlobalSettings.Add(solver_settings);
            GlobalStates.Add(new DualEndSolverState { AnchorsInitialized = false });

            AddChainToPhaseList(state, global_chain_index);
        }

        /*
         * Validates chain data needed during registration.
         *
         * @param chain_index Source chain index.
         * @param sub_chain Source chain data.
         * @param transform_map Source transform map.
         */
        private bool ValidateRegistrationInputs(
            int chain_index,
            IkSubChainData sub_chain,
            SubChainTransformMap transform_map)
        {
            if (transform_map == null)
            {
                DebugWarning("Register transform map is null.");
                return false;
            }

            if (transform_map.transformIndices == null)
            {
                DebugWarning("Register transform index list is null.");
                return false;
            }

            if (transform_map.jointStateIndices == null)
            {
                DebugWarning("Register joint state index list is null.");
                return false;
            }

            if (transform_map.boneStaticIndices == null)
            {
                DebugWarning("Register bone static index list is null.");
                return false;
            }

            if (sub_chain.jointStates == null)
            {
                DebugWarning("Register chain has no joint states.");
                return false;
            }

            if (sub_chain.boneStatics == null)
            {
                DebugWarning("Register chain has no bone statics.");
                return false;
            }

            int element_count = transform_map.transformIndices.Count;

            if (element_count < 2)
            {
                DebugWarning("Register chain has fewer than two elements.");
                return false;
            }

            if (transform_map.jointStateIndices.Count != element_count)
            {
                DebugWarning(
                    "Register joint state map count does not match element count. Chain " +
                    chain_index + " joint map " + transform_map.jointStateIndices.Count +
                    " elements " + element_count);

                return false;
            }

            if (transform_map.boneStaticIndices.Count != element_count)
            {
                DebugWarning(
                    "Register bone static map count does not match element count. Chain " +
                    chain_index + " bone map " + transform_map.boneStaticIndices.Count +
                    " elements " + element_count);

                return false;
            }

            return true;
        }

        /*
         * Registers the native transform index for one element.
         *
         * @param element_index Chain element index.
         * @param local_unique_index Local unique transform index.
         * @param unique_transform_offset Global unique transform offset.
         * @param unique_transform_count Number of unique transforms in the runner.
         */
        private int RegisterElementTransformIndex(
            int element_index,
            int local_unique_index,
            int unique_transform_offset,
            int unique_transform_count)
        {
            if (local_unique_index < 0)
            {
                GlobalTransformIndices.Add(-1);
                DebugWarning("Register local unique index is negative at element " + element_index + ".");
                return -1;
            }

            if (local_unique_index >= unique_transform_count)
            {
                GlobalTransformIndices.Add(-1);
                DebugWarning("Register local unique index is outside unique transform count at element " + element_index + ".");
                return -1;
            }

            int global_transform_index = local_unique_index + unique_transform_offset;
            GlobalTransformIndices.Add(global_transform_index);

            return global_transform_index;
        }

        /*
         * Gets the mapped joint state for one element.
         *
         * @param element_index Chain element index.
         * @param joint_state_index Source joint state index or negative.
         * @param sub_chain Source sub chain.
         */
        private JointState GetMappedJointState(
            int element_index,
            int joint_state_index,
            IkSubChainData sub_chain)
        {
            if (joint_state_index < 0)
            {
                return default(JointState);
            }

            if (joint_state_index >= sub_chain.jointStates.Count)
            {
                DebugWarning("Joint state index outside source list at element " + element_index + ".");
                return default(JointState);
            }

            return sub_chain.jointStates[joint_state_index];
        }

        /*
         * Gets the mapped bone static data for one element.
         *
         * @param element_index Chain element index.
         * @param bone_static_index Source bone static index or negative.
         * @param sub_chain Source sub chain.
         * @param transform_map Source transform map.
         * @param uniqueTransforms Source unique transform list.
         */
        private BoneStaticData GetMappedBoneStaticData(
            int element_index,
            int bone_static_index,
            IkSubChainData sub_chain,
            SubChainTransformMap transform_map,
            List<Transform> uniqueTransforms)
        {
            BoneStaticData bone_static_data = default(BoneStaticData);

            if (bone_static_index >= 0)
            {
                if (bone_static_index < sub_chain.boneStatics.Count)
                {
                    bone_static_data = sub_chain.boneStatics[bone_static_index];
                }
                else
                {
                    DebugWarning("Bone static index outside source list at element " + element_index + ".");
                }
            }

            float measured_length = MeasureElementDistance(
                element_index,
                transform_map,
                uniqueTransforms);

            if (bone_static_data.RestLength <= 0.000001f)
            {
                bone_static_data.RestLength = measured_length;
            }

            return bone_static_data;
        }

        /*
         * Measures distance between one path element and the next path element.
         *
         * @param element_index Chain element index.
         * @param transform_map Source transform map.
         * @param uniqueTransforms Source unique transform list.
         */
        private float MeasureElementDistance(
            int element_index,
            SubChainTransformMap transform_map,
            List<Transform> uniqueTransforms)
        {
            int next_element_index = element_index + 1;

            if (next_element_index >= transform_map.transformIndices.Count)
            {
                return 0f;
            }

            int current_unique_index = transform_map.transformIndices[element_index];
            int next_unique_index = transform_map.transformIndices[next_element_index];

            if (current_unique_index < 0)
            {
                return 0f;
            }

            if (next_unique_index < 0)
            {
                return 0f;
            }

            if (current_unique_index >= uniqueTransforms.Count)
            {
                return 0f;
            }

            if (next_unique_index >= uniqueTransforms.Count)
            {
                return 0f;
            }

            Transform current_transform = uniqueTransforms[current_unique_index];
            Transform next_transform = uniqueTransforms[next_unique_index];

            if (current_transform == null)
            {
                return 0f;
            }

            if (next_transform == null)
            {
                return 0f;
            }

            float measured_length = Vector3.Distance(current_transform.position, next_transform.position);

            return measured_length;
        }

        /*
         * Adds a global chain index to the correct phase list.
         *
         * @param state Chain state.
         * @param global_chain_index Global chain index.
         */
        private void AddChainToPhaseList(IKChainState state, int global_chain_index)
        {
            if (state.Phase == ChainPhase.Spine)
            {
                SpineIndices.Add(global_chain_index);
                return;
            }

            if (state.Phase == ChainPhase.Limbs)
            {
                LimbIndices.Add(global_chain_index);
                return;
            }

            if (state.Phase == ChainPhase.Tail)
            {
                TailIndices.Add(global_chain_index);
                return;
            }

            LimbIndices.Add(global_chain_index);
            DebugWarning("Unknown chain phase. Added chain to limb phase by default.");
        }

        /*
         * Disables a runner's chains and removes it from main thread sync.
         *
         * @param runner Runner to unregister.
         */
        public void UnregisterRunner(IkDualEndJobRunner runner)
        {
            if (!GlobalChains.IsCreated)
            {
                return;
            }

            RunnerSyncData target_data = null;

            for (int runner_index = 0; runner_index < activeRunners.Count; runner_index++)
            {
                RunnerSyncData sync_data = activeRunners[runner_index];

                if (sync_data == null)
                {
                    continue;
                }

                if (sync_data.Runner != runner)
                {
                    continue;
                }

                target_data = sync_data;
                activeRunners.RemoveAt(runner_index);
                break;
            }

            if (target_data == null)
            {
                return;
            }

            for (int chain_index = 0; chain_index < target_data.GlobalIndices.Count; chain_index++)
            {
                int global_chain_index = target_data.GlobalIndices[chain_index];

                if (global_chain_index < 0)
                {
                    continue;
                }

                if (global_chain_index >= GlobalChains.Length)
                {
                    continue;
                }

                IKChainState state = GlobalChains[global_chain_index];
                state.SolveWeight = 0f;
                GlobalChains[global_chain_index] = state;
            }

            DebugSystem("Runner unregistered.");
        }

        /*
         * Releases all native buffers owned by this system.
         */
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Application.quitting -= Dispose;

            if (GlobalChains.IsCreated)
            {
                GlobalChains.Dispose();
            }

            if (GlobalSettings.IsCreated)
            {
                GlobalSettings.Dispose();
            }

            if (GlobalStates.IsCreated)
            {
                GlobalStates.Dispose();
            }

            if (GlobalJoints.IsCreated)
            {
                GlobalJoints.Dispose();
            }

            if (GlobalBoneStatics.IsCreated)
            {
                GlobalBoneStatics.Dispose();
            }

            if (GlobalTransformIndices.IsCreated)
            {
                GlobalTransformIndices.Dispose();
            }

            if (WorkingSolvePositions.IsCreated)
            {
                WorkingSolvePositions.Dispose();
            }

            if (WorkingPreviousSolvePositions.IsCreated)
            {
                WorkingPreviousSolvePositions.Dispose();
            }

            if (WorkingOlderSolvePositions.IsCreated)
            {
                WorkingOlderSolvePositions.Dispose();
            }

            if (OutputWorldPositions.IsCreated)
            {
                OutputWorldPositions.Dispose();
            }

            if (OutputLocalRotations.IsCreated)
            {
                OutputLocalRotations.Dispose();
            }

            if (CurrentWorldRotations.IsCreated)
            {
                CurrentWorldRotations.Dispose();
            }

            if (SpineIndices.IsCreated)
            {
                SpineIndices.Dispose();
            }

            if (LimbIndices.IsCreated)
            {
                LimbIndices.Dispose();
            }

            if (TailIndices.IsCreated)
            {
                TailIndices.Dispose();
            }

            if (GlobalJointStatics.IsCreated)
            {
                GlobalJointStatics.Dispose();
            }

            if (WorkingBestSolvePositions.IsCreated)
            {
                WorkingBestSolvePositions.Dispose();
            }

            if (WorkingTempSolvePositions.IsCreated)
            {
                WorkingTempSolvePositions.Dispose();
            }

            activeRunners.Clear();
            isDisposed = true;

            DebugSystem("IK system disposed.");
        }

        /*
         * Prints first chain state before the job runs.
         */
        private void DebugFirstChainBeforeJob()
        {
            if (!debugEnabled)
            {
                return;
            }

            if (!ShouldDebugThisFrame())
            {
                return;
            }

            if (GlobalChains.Length == 0)
            {
                return;
            }

            IKChainState state = GlobalChains[0];

            DebugSystem(
                "Before job first chain target " + state.EndTargetPos +
                " start " + state.ElementStartIndex +
                " count " + state.ElementCount +
                " solve weight " + state.SolveWeight +
                " constraints " + state.ConstraintsEnabled);
        }

        /*
         * Prints first chain state after the job runs.
         */
        private void DebugFirstChainAfterJob()
        {
            if (!debugEnabled)
            {
                return;
            }

            if (!ShouldDebugThisFrame())
            {
                return;
            }

            if (GlobalChains.Length == 0)
            {
                return;
            }

            IKChainState state = GlobalChains[0];

            if (state.ElementCount < 2)
            {
                return;
            }

            int start = state.ElementStartIndex;
            int tip_index = start + state.ElementCount - 1;

            if (tip_index >= WorkingSolvePositions.Length)
            {
                return;
            }

            float tip_distance = math.distance(WorkingSolvePositions[tip_index], state.EndTargetPos);

            DebugSystem(
                "After job first chain tip " + WorkingSolvePositions[tip_index] +
                " target " + state.EndTargetPos +
                " distance " + tip_distance);
        }

        /*
         * Gets a solved position string for debug output.
         *
         * @param global_element_index Global element index.
         */
        private string GetDebugPosition(int global_element_index)
        {
            if (global_element_index < 0)
            {
                return "invalid";
            }

            if (global_element_index >= WorkingSolvePositions.Length)
            {
                return "invalid";
            }

            return WorkingSolvePositions[global_element_index].ToString();
        }

        /*
         * Returns true when debug output should be printed this frame.
         */
        private bool ShouldDebugThisFrame()
        {
            if (!debugEnabled)
            {
                return false;
            }

            if (debugEveryFrame)
            {
                return true;
            }

            if (debugFrameInterval <= 0)
            {
                return true;
            }

            if (tickCount % debugFrameInterval == 0)
            {
                return true;
            }

            return false;
        }

        /*
         * Prints a throttled debug message.
         *
         * @param message Message to print.
         */
        private void DebugSystemThrottled(string message)
        {
            if (!ShouldDebugThisFrame())
            {
                return;
            }

            DebugSystem(message);
        }

        /*
         * Prints a debug message.
         *
         * @param message Message to print.
         */
        private void DebugSystem(string message)
        {
            if (!debugEnabled)
            {
                return;
            }

            Debug.Log("IK System: " + message);
        }

        /*
         * Prints a warning message.
         *
         * @param message Message to print.
         */
        private void DebugWarning(string message)
        {
            if (!debugEnabled)
            {
                return;
            }

            Debug.LogWarning("IK System warning. " + message);
        }
    }
}
