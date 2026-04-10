// File: IKSystem/Safety/IkBuildSafety.cs
using UnityEngine;

namespace IKSystem.Safety
{
    public static class IkBuildSafety
    {
        /* Clamp to non-negative finite range.
         * @param value input value
         */
        public static float ClampNonNegativeFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            return value;
        }

        /* Check if a float is finite.
         * @param value input value
         */
        public static bool IsFinite(float value)
        {
            if (float.IsNaN(value))
            {
                return false;
            }

            if (float.IsInfinity(value))
            {
                return false;
            }

            return true;
        }

        /* Check if a vector is finite.
         * @param value input vector
         */
        public static bool IsFinite(Vector3 value)
        {
            if (!IsFinite(value.x))
            {
                return false;
            }

            if (!IsFinite(value.y))
            {
                return false;
            }

            if (!IsFinite(value.z))
            {
                return false;
            }

            return true;
        }

        /* Normalize with fallback.
         * @param value vector to normalize
         * @param fallback fallback vector
         * @param epsilon_value minimum magnitude
         */
        public static Vector3 SafeNormalize(Vector3 value, Vector3 fallback, float epsilon_value)
        {
            if (!IsFinite(value))
            {
                return fallback;
            }

            float magnitude = value.magnitude;
            if (!IsFinite(magnitude) || magnitude <= epsilon_value)
            {
                return fallback;
            }

            return value / magnitude;
        }

        /* Ensure a normal is not parallel to an axis.
         * @param normal candidate normal
         * @param axis axis direction
         * @param fallback_normal fallback normal
         */
        public static Vector3 EnsureNotParallel(Vector3 normal, Vector3 axis, Vector3 fallback_normal)
        {
            float dot = Vector3.Dot(normal, axis);
            if (!IsFinite(dot))
            {
                return fallback_normal;
            }

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

        /* Validate a transform path for nulls, minimum length, and duplicates.
         * @param path candidate path
         * @param error_message failure reason
         */
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
