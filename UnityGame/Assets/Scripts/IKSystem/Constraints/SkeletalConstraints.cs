using IKSystem.Data;
using IKSystem.Contracts;
using UnityEngine;
using System.Diagnostics;

namespace IKSystem.Constraints
{
    public sealed class SkeletalConstraints : ISkeletalConstraints
    {
        private readonly float epsilon;

        public SkeletalConstraints(float epsilon_value)
        {
            epsilon = Mathf.Max(1e-12f, epsilon_value);
        }


        public void Bone_Length(
            ref BoneEndpoints joints,
            in BoneConstraint bone,
            Vector3 fallback_direction)
        {
            Debug_Assert_Finite(joints.Parent, "Parent position");
            Debug_Assert_Finite(joints.Child, "Child position");
            Debug_Assert_Finite(fallback_direction, "Fallback direction");

            Vector3 delta = joints.Child - joints.Parent;
            float distance = delta.magnitude;

            Debug_Assert_Finite(delta, "Delta");
            Debug_Assert_Finite(distance, "Distance");

            Vector3 direction_normal;
            if (distance > epsilon)
            {
                direction_normal = delta / distance;
            }
            else
            {
                float fallback = fallback_direction.magnitude;
                direction_normal = (fallback > epsilon)
                    ? (fallback_direction / fallback)
                    : Vector3.right;
            }

            Debug_Assert_Finite(direction_normal, "Direction normal");

            float error = distance - bone.Length;
            if (Mathf.Abs(error) <= bone.Tolerance)
            {
                return;
            }

            float parent_share = Mathf.Clamp01(bone.ParentShare);
            float child_share = Mathf.Clamp01(bone.ChildShare);

            float total = parent_share + child_share;
            if (total > epsilon)
            {
                parent_share /= total;
                child_share /= total;
            }

            Vector3 correction = direction_normal * error;

            Debug_Assert_Finite(correction, "Correction");

            joints.Parent += correction * parent_share;
            joints.Child -= correction * child_share;

            Debug_Assert_Finite(joints.Parent, "Parent result");
            Debug_Assert_Finite(joints.Child, "Child result");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void Debug_Assert_Finite(Vector3 value, string label)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z))
            {
                UnityEngine.Debug.LogWarning(label + " was not finite.");
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void Debug_Assert_Finite(float value, string label)
        {
            if (!float.IsFinite(value))
            {
                UnityEngine.Debug.LogWarning(label + " was not finite.");
            }
        }
    }
}
