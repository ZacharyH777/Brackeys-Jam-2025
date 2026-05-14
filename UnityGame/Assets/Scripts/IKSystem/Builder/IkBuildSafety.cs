using UnityEngine;

namespace RunstarSystems.IKSystem.Safety
{
    public static class IkBuildSafety
    {
        public static float ClampNonNegativeFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }
            return value;
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static Vector3 SafeNormalize(Vector3 value, Vector3 fallback, float epsilon_value)
        {
            if (!IsFinite(value)) return fallback;

            float magnitude = value.magnitude;
            if (!IsFinite(magnitude) || magnitude <= epsilon_value)
            {
                return fallback;
            }

            return value / magnitude;
        }

        public static Vector3 EnsureNotParallel(Vector3 normal, Vector3 axis, Vector3 fallback_normal)
        {
            float dot = Vector3.Dot(normal, axis);
            if (!IsFinite(dot)) return fallback_normal;

            float abs_dot = Mathf.Abs(dot);
            if (abs_dot > 0.99f)
            {
                Vector3 alternative = Vector3.Cross(axis, Vector3.up);
                if (alternative == Vector3.zero)
                {
                    alternative = Vector3.Cross(axis, Vector3.right);
                }
                return alternative;
            }

            return normal;
        }

        public static bool ValidatePath(Transform[] path, out string error_message)
        {
            error_message = null;

            if (path == null || path.Length < 2)
            {
                error_message = "Invalid chain path.";
                return false;
            }

            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null)
                {
                    error_message = "Chain path contained a missing transform.";
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (path[j] == path[i])
                    {
                        error_message = "Chain path contained a duplicate transform.";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
