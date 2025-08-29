using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/*
* Optimized FABRIK dual-direction solver job with Burst compilation.
* Supports both tip-first and root-first pass ordering with configurable relaxation.
* Operates in 2D (XY plane) with Z preservation for Unity's 2D workflows.
*/

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast)]
public struct IkDuelDirectionJob : IJob
{
    // Input data (read-only)
    [ReadOnly] public NativeArray<float3> p_ls;
    [ReadOnly] public NativeArray<float> segment_lengths;
    [ReadOnly] public float3 anchor_a_ls;      // Tip target (end effector)
    [ReadOnly] public float3 anchor_b_ls;      // Root target (base)
    [ReadOnly] public float relaxation;        // Blend factor [0,1]
    [ReadOnly] public int count;               // Number of joints
    
    // Output data
    public NativeArray<float3> p_out_ls;

    // Constants for optimization
    private const float DIRECTION_EPSILON = 1e-16f;

    public void Execute()
    {
        // Early exit for invalid data
        if (count < 2 || !p_ls.IsCreated || !p_out_ls.IsCreated || !segment_lengths.IsCreated)
            return;

        // Copy input positions to output
        for (int i = 0; i < count; i++)
            p_out_ls[i] = p_ls[i];

        // Execute tip pass first (backward)
        ExecuteTipPass();
        
        // Then execute root pass (forward)
        ExecuteRootPass();
    }

    /// <summary>
    /// Backward pass: from tip (anchor_a) toward root
    /// Constrains the tip to anchor_a and propagates constraints backward
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ExecuteTipPass()
    {
        // Snap tip to target anchor
        p_out_ls[count - 1] = SnapXY(p_out_ls[count - 1], anchor_a_ls);

        // Backward propagation from tip to root
        for (int i = count - 2; i >= 0; i--)
        {
            // Calculate direction from current joint to child
            float3 dir = SafeDirPlanar(p_out_ls[i] - p_out_ls[i + 1]);
            
            // Calculate target position maintaining segment length
            float3 target = new float3(
                p_out_ls[i + 1].x + dir.x * segment_lengths[i],
                p_out_ls[i + 1].y + dir.y * segment_lengths[i],
                p_out_ls[i].z  // Preserve Z coordinate
            );
            
            // Apply relaxation blending
            p_out_ls[i] = math.lerp(p_out_ls[i], target, relaxation);
        }
    }

    /// <summary>
    /// Forward pass: from root (anchor_b) toward tip
    /// Constrains the root to anchor_b and propagates constraints forward
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ExecuteRootPass()
    {
        // Snap root to target anchor
        p_out_ls[0] = SnapXY(p_out_ls[0], anchor_b_ls);

        // Forward propagation from root to tip
        for (int i = 0; i < count - 1; i++)
        {
            // Calculate direction from parent to current joint
            float3 dir = SafeDirPlanar(p_out_ls[i + 1] - p_out_ls[i]);
            
            // Calculate target position maintaining segment length
            float3 target = new float3(
                p_out_ls[i].x + dir.x * segment_lengths[i],
                p_out_ls[i].y + dir.y * segment_lengths[i],
                p_out_ls[i + 1].z  // Preserve Z coordinate
            );
            
            // Apply relaxation blending
            p_out_ls[i + 1] = math.lerp(p_out_ls[i + 1], target, relaxation);
        }
    }

    /// <summary>
    /// Snaps the XY coordinates of original to source while preserving Z
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float3 SnapXY(float3 original, float3 source)
    {
        return new float3(source.x, source.y, original.z);
    }

    /// <summary>
    /// Safely normalizes a vector in the XY plane with fallback for zero-length vectors
    /// Uses fast reciprocal square root for performance
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float3 SafeDirPlanar(float3 v)
    {
        // Force planar (XY only)
        v.z = 0f;
        
        // Calculate squared length for efficiency
        float lengthSq = v.x * v.x + v.y * v.y;
        
        // Use fast reciprocal square root if vector is significant
        if (lengthSq > DIRECTION_EPSILON)
        {
            float invLength = math.rsqrt(lengthSq);
            return new float3(v.x * invLength, v.y * invLength, 0f);
        }
        
        // Fallback to unit X direction for zero/near-zero vectors
        return new float3(1f, 0f, 0f);
    }

    /// <summary>
    /// Calculate squared distance in XY plane (avoiding sqrt for performance)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float DistanceSqXY(float3 a, float3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Calculate distance in XY plane
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float DistanceXY(float3 a, float3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return math.sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Validates that all segment lengths are positive and reasonable
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ValidateSegmentLengths()
    {
        for (int i = 0; i < count - 1; i++)
        {
            if (segment_lengths[i] <= 0f || !math.isfinite(segment_lengths[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Calculate total chain length for validation purposes
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private float CalculateTotalLength()
    {
        float total = 0f;
        for (int i = 0; i < count - 1; i++)
            total += segment_lengths[i];
        return total;
    }
}

/// <summary>
/// Extended version with additional features and configurability
/// </summary>
[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct IkDuelDirectionJobExtended : IJob
{
    [ReadOnly] public NativeArray<float3> p_ls;
    [ReadOnly] public NativeArray<float> segment_lengths;
    [ReadOnly] public float3 anchor_a_ls;
    [ReadOnly] public float3 anchor_b_ls;
    [ReadOnly] public float relaxation;
    [ReadOnly] public int count;
    
    // Extended parameters
    [ReadOnly] public bool do_tip_first;
    [ReadOnly] public float convergence_threshold;
    [ReadOnly] public int max_sub_iterations;
    [ReadOnly] public bool enable_length_preservation;
    [ReadOnly] public float damping_factor;
    
    public NativeArray<float3> p_out_ls;
    
    // Optional output metrics
    public NativeArray<float> convergence_metrics;  // [0] = final error, [1] = iterations used

    public void Execute()
    {
        if (count < 2) return;

        // Initialize output with input
        for (int i = 0; i < count; i++)
            p_out_ls[i] = p_ls[i];

        float initial_error = CalculateError();
        int iterations_used = 0;

        // Sub-iteration loop for convergence
        for (int iter = 0; iter < max_sub_iterations; iter++)
        {
            // Store previous state for damping
            NativeArray<float3> prev_state = new NativeArray<float3>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                prev_state[i] = p_out_ls[i];

            // Execute primary passes
            if (do_tip_first)
            {
                ExecuteTipPassExtended();
                ExecuteRootPassExtended();
            }
            else
            {
                ExecuteRootPassExtended();
                ExecuteTipPassExtended();
            }

            // Apply damping if enabled
            if (damping_factor > 0f && damping_factor < 1f)
            {
                for (int i = 0; i < count; i++)
                {
                    p_out_ls[i] = math.lerp(prev_state[i], p_out_ls[i], 1f - damping_factor);
                }
            }

            // Length preservation pass
            if (enable_length_preservation)
                PreserveLengths();

            prev_state.Dispose();
            iterations_used++;

            // Check convergence
            float current_error = CalculateError();
            if (current_error < convergence_threshold)
                break;
        }

        // Store metrics if array is provided
        if (convergence_metrics.IsCreated && convergence_metrics.Length >= 2)
        {
            convergence_metrics[0] = CalculateError();
            convergence_metrics[1] = iterations_used;
        }
    }

    private void ExecuteTipPassExtended()
    {
        p_out_ls[count - 1] = SnapXY(p_out_ls[count - 1], anchor_a_ls);

        for (int i = count - 2; i >= 0; i--)
        {
            float3 dir = SafeDirPlanar(p_out_ls[i] - p_out_ls[i + 1]);
            float3 target = new float3(
                p_out_ls[i + 1].x + dir.x * segment_lengths[i],
                p_out_ls[i + 1].y + dir.y * segment_lengths[i],
                p_out_ls[i].z
            );
            p_out_ls[i] = math.lerp(p_out_ls[i], target, relaxation);
        }
    }

    private void ExecuteRootPassExtended()
    {
        p_out_ls[0] = SnapXY(p_out_ls[0], anchor_b_ls);

        for (int i = 0; i < count - 1; i++)
        {
            float3 dir = SafeDirPlanar(p_out_ls[i + 1] - p_out_ls[i]);
            float3 target = new float3(
                p_out_ls[i].x + dir.x * segment_lengths[i],
                p_out_ls[i].y + dir.y * segment_lengths[i],
                p_out_ls[i + 1].z
            );
            p_out_ls[i + 1] = math.lerp(p_out_ls[i + 1], target, relaxation);
        }
    }

    private void PreserveLengths()
    {
        // Ensure all segments maintain their original lengths
        for (int i = 0; i < count - 1; i++)
        {
            float3 current_vec = p_out_ls[i + 1] - p_out_ls[i];
            float current_length = math.length(new float3(current_vec.x, current_vec.y, 0f));
            
            if (current_length > 1e-8f)
            {
                float scale = segment_lengths[i] / current_length;
                float3 corrected = p_out_ls[i] + new float3(
                    current_vec.x * scale,
                    current_vec.y * scale,
                    current_vec.z
                );
                p_out_ls[i + 1] = new float3(corrected.x, corrected.y, p_out_ls[i + 1].z);
            }
        }
    }

    private float CalculateError()
    {
        float tip_error = math.distance(new float3(p_out_ls[count - 1].x, p_out_ls[count - 1].y, 0f),
                                       new float3(anchor_a_ls.x, anchor_a_ls.y, 0f));
        float root_error = math.distance(new float3(p_out_ls[0].x, p_out_ls[0].y, 0f),
                                        new float3(anchor_b_ls.x, anchor_b_ls.y, 0f));
        return tip_error + root_error;
    }

    private static float3 SnapXY(float3 original, float3 source)
    {
        return new float3(source.x, source.y, original.z);
    }

    private static float3 SafeDirPlanar(float3 v)
    {
        v.z = 0f;
        float lengthSq = v.x * v.x + v.y * v.y;
        if (lengthSq > 1e-16f)
            return v * math.rsqrt(lengthSq);
        return new float3(1f, 0f, 0f);
    }
}

/// <summary>
/// Parallel version for processing multiple chains simultaneously
/// </summary>
[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct IkDuelDirectionParallelJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> chain_starts;        // Start index for each chain
    [ReadOnly] public NativeArray<int> chain_lengths;       // Length of each chain
    [ReadOnly] public NativeArray<float3> all_positions;    // All joint positions
    [ReadOnly] public NativeArray<float> all_segment_lengths;
    [ReadOnly] public NativeArray<float3> tip_anchors;      // Tip targets for each chain
    [ReadOnly] public NativeArray<float3> root_anchors;     // Root targets for each chain
    [ReadOnly] public float relaxation;
    
    public NativeArray<float3> output_positions;

    public void Execute(int chain_index)
    {
        int start = chain_starts[chain_index];
        int length = chain_lengths[chain_index];
        
        if (length < 2) return;

        // Extract chain data
        float3 tip_anchor = tip_anchors[chain_index];
        float3 root_anchor = root_anchors[chain_index];

        // Copy input to output for this chain
        for (int i = 0; i < length; i++)
            output_positions[start + i] = all_positions[start + i];

        // Tip pass (backward)
        output_positions[start + length - 1] = SnapXY(output_positions[start + length - 1], tip_anchor);
        
        for (int i = length - 2; i >= 0; i--)
        {
            int abs_i = start + i;
            float3 dir = SafeDirPlanar(output_positions[abs_i] - output_positions[abs_i + 1]);
            float3 target = new float3(
                output_positions[abs_i + 1].x + dir.x * all_segment_lengths[abs_i],
                output_positions[abs_i + 1].y + dir.y * all_segment_lengths[abs_i],
                output_positions[abs_i].z
            );
            output_positions[abs_i] = math.lerp(output_positions[abs_i], target, relaxation);
        }

        // Root pass (forward)
        output_positions[start] = SnapXY(output_positions[start], root_anchor);
        
        for (int i = 0; i < length - 1; i++)
        {
            int abs_i = start + i;
            float3 dir = SafeDirPlanar(output_positions[abs_i + 1] - output_positions[abs_i]);
            float3 target = new float3(
                output_positions[abs_i].x + dir.x * all_segment_lengths[abs_i],
                output_positions[abs_i].y + dir.y * all_segment_lengths[abs_i],
                output_positions[abs_i + 1].z
            );
            output_positions[abs_i + 1] = math.lerp(output_positions[abs_i + 1], target, relaxation);
        }
    }

    private static float3 SnapXY(float3 original, float3 source)
    {
        return new float3(source.x, source.y, original.z);
    }

    private static float3 SafeDirPlanar(float3 v)
    {
        v.z = 0f;
        float lengthSq = v.x * v.x + v.y * v.y;
        if (lengthSq > 1e-16f)
            return v * math.rsqrt(lengthSq);
        return new float3(1f, 0f, 0f);
    }
}