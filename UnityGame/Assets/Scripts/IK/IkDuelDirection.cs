using System;
using UnityEngine;
using ik_data;

/*
* FABRIK 2D dual-end solver; rotates pivots around local Z.
* Adds a pull toward a user-defined Transform line to reduce cycling.
*
*/

[DisallowMultipleComponent]
[RequireComponent(typeof(IkChainBuilder))]
[AddComponentMenu("IK/Solver/IK Dual-End 2D")]
public sealed class IkDualEndEffectorSolver : MonoBehaviour
{
    public enum SolveMode { TipFirst, RootFirst, Alternate }

    [Header("Chain Source")]
    [Tooltip("Serialized chain to drive")]
    public IkChainBuilder ik_builder;

    [Header("Targets")]
    [Tooltip("Tip goal")]
    public Transform target_tip_a;
    [Tooltip("Root goal")]
    public Transform target_root_b;

    [Header("Solve Space")]
    [Tooltip("Override space (else root.parent)")]
    public Transform solve_space_override;
    [Tooltip("Stamp root XY in solve space")]
    public bool pin_root_in_solve_space = true;

    [Header("2D Settings")]
    [Tooltip("Limit solve to XY")]
    public bool planar_2d = true;
    [Tooltip("Bone forward axis (local)")]
    public Vector3 bone_forward_local = Vector3.right;

    [Header("Solver Controls")]
    [Tooltip("Iteration order")]
    public SolveMode solve_mode = SolveMode.Alternate;
    [Min(1), Tooltip("FABRIK iterations")]
    public int iterations = 10;
    [Min(0f), Tooltip("Stop distance")]
    public float tolerance = 1e-3f;
    [Range(0.05f, 1f), Tooltip("FABRIK blend")]
    public float relaxation = 0.6f;

    [Header("Application Mode")]
    [Tooltip("Rotate only; no position stamping")]
    public bool rotation_only = true;
    [Tooltip("Temporarily set scale to (1,1,1) while rotating")]
    public bool unscale_during_rotation = true;
    [Tooltip("Prefer joint.optional_transform")]
    public bool use_joint_optional_transform = true;
    [Range(1f, 60f), Tooltip("Per-joint Z clamp (deg)")]
    public float max_delta_z_deg = 20f;

    [Header("Target Smoothing")]
    [Range(0f, 0.95f), Tooltip("Smooth target jitter")]
    public float target_smoothing = 0f;

    [Header("Pull")]
    [Tooltip("Attract along root → pull_target")]
    public Transform pull_target;
    [Range(0f, 1f), Tooltip("Pull strength")]
    public float pull_strength = 0.35f;
    [Tooltip("-1=root bias, 0=uniform, +1=pull end bias")]
    [Range(-1f, 1f)] public float pull_bias = 0.5f;
    [Tooltip("Extra pull passes per iteration")]
    [Range(0, 3)] public int pull_passes = 1;

    [Header("Update")]
    [Tooltip("Run in LateUpdate (Play)")]
    public bool solve_in_play_mode = true;

    [Header("Debug")]
    [Tooltip("Draw bones and targets")]
    public bool debug_draw;
    [Tooltip("Gizmo color")]
    public Color debug_color = new(0.2f, 1f, 0.6f, 1f);

    private Vector3[] p_ls;
    private float[] segment_lengths;
    private float total_len;
    private int count;
    private bool initialized;
    private Transform solve_space;
    private Vector3 anchor_a_prev_ls, anchor_b_prev_ls;
    private bool anchors_init;

    /*
    * Unity lifecycle
    *
    */
    void Awake()
    {
        if (!ik_builder) ik_builder = GetComponent<IkChainBuilder>();
    }

    /*
    * Unity lifecycle
    *
    */
    void OnEnable()
    {
        if (!ik_builder) ik_builder = GetComponent<IkChainBuilder>();
        initialized = false;
    }

    /*
    * Unity lifecycle
    *
    */
    void LateUpdate()
    {
        if (!Application.isPlaying || !solve_in_play_mode) return;
        SolveNow();
    }

    /*
    * Execute one FABRIK solve and apply local-Z rotations with pull
    *
    */
    public void SolveNow()
    {
        if (!Application.isPlaying) return;
        if (!ik_builder) ik_builder = GetComponent<IkChainBuilder>();

        var chain = ik_builder.ChainRO;
        if (chain.bone_chain == null || chain.bone_chain.Count < 2) return;

        Transform root_transform = chain.bone_chain[0].transform;

        Transform desired_space = null;
        if (solve_space_override != null) desired_space = solve_space_override;
        else if (root_transform != null) desired_space = root_transform.parent;

        if (!initialized || solve_space != desired_space || count != chain.bone_chain.Count || p_ls == null || segment_lengths == null)
            InitFromChain(chain, desired_space);

        for (int i = 0; i < count; i++)
        {
            Transform bone_i_transform = chain.bone_chain[i].transform;
            if (!bone_i_transform) continue;
            p_ls[i] = ToSpacePoint(bone_i_transform.position);
        }

        Vector3 tip_now_world;
        Transform tip_transform = chain.bone_chain[count - 1].transform;
        if (tip_transform != null) tip_now_world = tip_transform.position;
        else tip_now_world = FromSpacePoint(p_ls[count - 1]);

        Vector3 root_now_world;
        if (root_transform != null) root_now_world = root_transform.position;
        else root_now_world = FromSpacePoint(p_ls[0]);

        Vector3 anchor_a_ls;
        if (target_tip_a != null) anchor_a_ls = ToSpacePoint(target_tip_a.position);
        else anchor_a_ls = ToSpacePoint(tip_now_world);

        Vector3 anchor_b_ls;
        if (target_root_b != null) anchor_b_ls = ToSpacePoint(target_root_b.position);
        else anchor_b_ls = ToSpacePoint(root_now_world);

        if (!anchors_init)
        {
            anchor_a_prev_ls = anchor_a_ls;
            anchor_b_prev_ls = anchor_b_ls;
            anchors_init = true;
        }

        float smoothing_blend = 1f - target_smoothing;
        anchor_a_ls = Vector3.Lerp(anchor_a_prev_ls, anchor_a_ls, smoothing_blend);
        anchor_b_ls = Vector3.Lerp(anchor_b_prev_ls, anchor_b_ls, smoothing_blend);
        anchor_a_prev_ls = anchor_a_ls;
        anchor_b_prev_ls = anchor_b_ls;

        float anchors_dist = Dist2D(anchor_a_ls, anchor_b_ls);
        if (anchors_dist > total_len)
        {
            StraightenBetween(anchor_b_ls, anchor_a_ls);
            ApplyRotations(chain);
            return;
        }

        Vector3 pull_end_ls = anchor_a_ls;
        if (pull_target != null) pull_end_ls = ToSpacePoint(pull_target.position);

        for (int it = 0; it < iterations; it++)
        {
            if (solve_mode == SolveMode.RootFirst)
            {
                RootPass(anchor_b_ls);
                TipPass(anchor_a_ls);
            }
            else if (solve_mode == SolveMode.TipFirst)
            {
                TipPass(anchor_a_ls);
                RootPass(anchor_b_ls);
            }
            else
            {
                if ((it & 1) == 0) { TipPass(anchor_a_ls); RootPass(anchor_b_ls); }
                else               { RootPass(anchor_b_ls); TipPass(anchor_a_ls); }
            }

            for (int p = 0; p < pull_passes; p++)
            {
                TensionPass(anchor_b_ls, pull_end_ls);
            }

            float e_a = Dist2D(p_ls[count - 1], anchor_a_ls);
            float e_b = Dist2D(p_ls[0],        anchor_b_ls);
            if (e_a <= tolerance && e_b <= tolerance) break;
        }

        if (pin_root_in_solve_space && target_root_b && chain.bone_chain[0].transform)
        {
            Transform root_bone_transform = chain.bone_chain[0].transform;
            Vector3 current_ls = ToSpacePoint(root_bone_transform.position);
            current_ls.x = anchor_b_ls.x;
            current_ls.y = anchor_b_ls.y;
            root_bone_transform.position = FromSpacePoint(current_ls);
        }

        ApplyRotations(chain);
    }

    /*
    * Backward pass from tip to root in solve space
    *
    */
    private void TipPass(in Vector3 anchor_a_ls)
    {
        p_ls[count - 1] = SnapXY(p_ls[count - 1], anchor_a_ls);

        for (int i = count - 2; i >= 0; i--)
        {
            Vector3 dir = SafeDirPlanar(p_ls[i] - p_ls[i + 1]);
            Vector3 target = new Vector3(
                p_ls[i + 1].x + dir.x * segment_lengths[i],
                p_ls[i + 1].y + dir.y * segment_lengths[i],
                p_ls[i].z
            );
            p_ls[i] = Vector3.Lerp(p_ls[i], target, relaxation);
        }
    }

    /*
    * Forward pass from root to tip in solve space
    *
    */
    private void RootPass(in Vector3 anchor_b_ls)
    {
        p_ls[0] = SnapXY(p_ls[0], anchor_b_ls);

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 dir = SafeDirPlanar(p_ls[i + 1] - p_ls[i]);
            Vector3 target = new Vector3(
                p_ls[i].x + dir.x * segment_lengths[i],
                p_ls[i].y + dir.y * segment_lengths[i],
                p_ls[i + 1].z
            );
            p_ls[i + 1] = Vector3.Lerp(p_ls[i + 1], target, relaxation);
        }
    }

    /*
    * Straighten between two anchors in solve space
    *
    */
    private void StraightenBetween(in Vector3 root_ls, in Vector3 tip_ls)
    {
        Vector3 dir = SafeDirPlanar(tip_ls - root_ls);
        p_ls[0] = new Vector3(root_ls.x, root_ls.y, p_ls[0].z);

        for (int i = 0; i < count - 1; i++)
        {
            p_ls[i + 1] = new Vector3(
                p_ls[i].x + dir.x * segment_lengths[i],
                p_ls[i].y + dir.y * segment_lengths[i],
                p_ls[i + 1].z
            );
        }

        p_ls[count - 1] = new Vector3(tip_ls.x, tip_ls.y, p_ls[count - 1].z);
    }

    /*
    * Pull joints toward the line root_ls → pull_end_ls
    *
    */
    private void TensionPass(in Vector3 root_ls, in Vector3 pull_end_ls)
    {
        if (pull_strength <= 0f || pull_passes <= 0) return;

        Vector3 line = pull_end_ls - root_ls;
        if (planar_2d) line.z = 0f;

        float denom = line.x * line.x + line.y * line.y;
        if (denom < 1e-12f) return;

        for (int i = 1; i < count - 1; i++)
        {
            Vector3 pi = p_ls[i];
            Vector3 v = new Vector3(pi.x - root_ls.x, pi.y - root_ls.y, 0f);
            float t = (v.x * line.x + v.y * line.y) / denom;

            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            Vector3 proj = new Vector3(root_ls.x + line.x * t, root_ls.y + line.y * t, pi.z);

            float idx01 = (float)i / (float)(count - 1);
            float bias_w = 1f;
            if (pull_bias > 0f) bias_w = Mathf.Lerp(1f, idx01, pull_bias);
            else if (pull_bias < 0f) bias_w = Mathf.Lerp(1f, 1f - idx01, -pull_bias);

            float w = pull_strength * bias_w;
            p_ls[i] = Vector3.Lerp(pi, proj, w);
        }
    }

    /*
    * Initialize working buffers from chain and choose solve space
    *
    */
    private void InitFromChain(IkChain chain, Transform desired_space)
    {
        solve_space = desired_space;
        count = chain.bone_chain.Count;
        p_ls = new Vector3[count];
        segment_lengths = new float[count - 1];
        total_len = 0f;

        for (int i = 0; i < count - 1; i++)
        {
            Transform parent_bone_transform = chain.bone_chain[i].transform;
            Transform child_bone_transform = chain.bone_chain[i + 1].transform;

            Vector3 parent_bone_ls;
            if (parent_bone_transform != null) parent_bone_ls = ToSpacePoint(parent_bone_transform.position);
            else parent_bone_ls = Vector3.zero;

            Vector3 child_bone_ls;
            if (child_bone_transform != null) child_bone_ls = ToSpacePoint(child_bone_transform.position);
            else
            {
                float fallback_y = Mathf.Max(0.1f, i == 0 ? 0.1f : segment_lengths[i - 1]);
                child_bone_ls = new Vector3(0f, fallback_y, 0f);
            }

            float len = Dist2D(parent_bone_ls, child_bone_ls);
            if (len <= 1e-6f)
            {
                if (i > 0) len = segment_lengths[i - 1];
                else len = 0.1f;
            }

            segment_lengths[i] = len;
            total_len += len;
        }

        for (int i = 0; i < count; i++)
        {
            Transform bone_i_transform = chain.bone_chain[i].transform;
            if (bone_i_transform != null) p_ls[i] = ToSpacePoint(bone_i_transform.position);
            else p_ls[i] = Vector3.zero;
        }

        initialized = true;
    }

    /*
    * Apply Z-only local rotations toward solved segment directions
    *
    */
    private void ApplyRotations(IkChain chain)
    {
        if (!rotation_only)
        {
            for (int i = 0; i < count; i++)
            {
                Transform bone_i_transform = chain.bone_chain[i].transform;
                if (!bone_i_transform) continue;

                Vector3 current_ls = ToSpacePoint(bone_i_transform.position);
                current_ls.x = p_ls[i].x;
                current_ls.y = p_ls[i].y;
                bone_i_transform.position = FromSpacePoint(current_ls);
            }
        }

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 desired_dir_ls = SafeDirPlanar(p_ls[i + 1] - p_ls[i]);
            if (desired_dir_ls.sqrMagnitude < 1e-10f) continue;

            Vector3 desired_dir_world = FromSpaceDir(desired_dir_ls);

            Transform pivot_transform = null;
            if (use_joint_optional_transform && chain.joint_chain != null && i < chain.joint_chain.Count)
                pivot_transform = chain.joint_chain[i].optional_transform;
            if (!pivot_transform) pivot_transform = chain.bone_chain[i].transform;
            if (!pivot_transform) continue;

            Transform parent_transform = pivot_transform.parent;

            Transform child_transform = chain.bone_chain[i + 1].transform;
            if (!child_transform) continue;

            Vector3 current_dir_world = child_transform.position - pivot_transform.position;
            current_dir_world.z = 0f;
            if (current_dir_world.sqrMagnitude < 1e-10f) continue;

            Vector3 desired_local;
            if (parent_transform != null) desired_local = parent_transform.InverseTransformDirection(desired_dir_world);
            else desired_local = desired_dir_world;

            Vector3 current_local;
            if (parent_transform != null) current_local = parent_transform.InverseTransformDirection(current_dir_world);
            else current_local = current_dir_world;

            desired_local.z = 0f;
            current_local.z = 0f;
            if (desired_local.sqrMagnitude < 1e-10f || current_local.sqrMagnitude < 1e-10f) continue;

            desired_local.Normalize();
            current_local.Normalize();

            float delta_z = Vector2.SignedAngle(
                new Vector2(current_local.x, current_local.y),
                new Vector2(desired_local.x, desired_local.y)
            );

            if (delta_z > max_delta_z_deg) delta_z = max_delta_z_deg;
            else if (delta_z < -max_delta_z_deg) delta_z = -max_delta_z_deg;

            if (Mathf.Abs(delta_z) < 1e-4f) continue;

            Vector3 saved_scale = pivot_transform.localScale;
            if (unscale_during_rotation) pivot_transform.localScale = Vector3.one;

            pivot_transform.localRotation = Quaternion.AngleAxis(delta_z, Vector3.forward) * pivot_transform.localRotation;

            if (unscale_during_rotation) pivot_transform.localScale = saved_scale;
        }

        ref EndEffector eff = ref ik_builder.GetEffectorRef();
        Transform tip_transform_final = chain.bone_chain[count - 1].transform;
        if (tip_transform_final)
        {
            eff.location = tip_transform_final.position;
            eff.rotation = tip_transform_final.rotation;
        }
    }

    /*
    * Convert world point to solve space
    *
    */
    private Vector3 ToSpacePoint(Vector3 world_point)
    {
        if (solve_space != null) return solve_space.InverseTransformPoint(world_point);
        return world_point;
    }

    /*
    * Convert solve-space point to world
    *
    */
    private Vector3 FromSpacePoint(Vector3 space_point)
    {
        if (solve_space != null) return solve_space.TransformPoint(space_point);
        return space_point;
    }

    /*
    * Convert world dir to solve space
    *
    */
    private Vector3 ToSpaceDir(Vector3 world_dir)
    {
        if (solve_space != null) return solve_space.InverseTransformDirection(world_dir);
        return world_dir;
    }

    /*
    * Convert solve-space dir to world
    *
    */
    private Vector3 FromSpaceDir(Vector3 space_dir)
    {
        if (solve_space != null) return solve_space.TransformDirection(space_dir);
        return space_dir;
    }

    /*
    * Utility: snap XY, keep Z
    *
    */
    private static Vector3 SnapXY(Vector3 original, Vector3 source)
    {
        return new Vector3(source.x, source.y, original.z);
    }

    /*
    * Utility: 2D distance
    *
    */
    private static float Dist2D(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /*
    * Utility: normalized vector in XY (optionally zeroing Z)
    *
    */
    private Vector3 SafeDirPlanar(Vector3 v)
    {
        if (planar_2d) v.z = 0f;
        float m = v.magnitude;
        if (m > 1e-8f) return v / m;
        return Vector3.right;
    }

#if UNITY_EDITOR
    /*
    * Debug gizmos
    *
    */
    void OnDrawGizmosSelected()
    {
        if (!debug_draw || ik_builder == null) return;
        var chain = ik_builder.ChainRO;
        if (chain.bone_chain == null || chain.bone_chain.Count < 2) return;

        Gizmos.color = debug_color;
        for (int i = 0; i < chain.bone_chain.Count - 1; i++)
        {
            Transform bone_a = chain.bone_chain[i].transform;
            Transform bone_b = chain.bone_chain[i + 1].transform;
            if (!bone_a || !bone_b) continue;
            Gizmos.DrawLine(bone_a.position, bone_b.position);
        }

        if (target_root_b) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(target_root_b.position, 0.03f); }
        if (target_tip_a)  { Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(target_tip_a.position,  0.03f); }
        if (pull_target)   { Gizmos.color = Color.magenta;Gizmos.DrawWireSphere(pull_target.position,   0.03f); }
    }
#endif
}
