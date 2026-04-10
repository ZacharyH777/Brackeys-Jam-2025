using System;
using UnityEngine;

namespace IKSystem.Data
{
    [Serializable]
    public struct BoneConstraint
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Rest length of the bone")]
        private float length;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Acceptable error before correction")]
        private float tolerance;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("0 = parent pinned, 1 = child pinned")]
        private float weight;

        public float Length
        {
            get { return length; }
            set { length = clamp_min(value); }
        }

        public float Tolerance
        {
            get { return tolerance; }
            set { tolerance = clamp_min(value); }
        }

        public float Weight
        {
            get { return weight; }
            set { weight = clamp_range(value); }
        }

        public float ParentShare
        {
            get { return weight; }
        }

        public float ChildShare
        {
            get { return 1f - weight; }
        }

        private static float clamp_min(float value)
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

        private static float clamp_range(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.5f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    [Serializable]
    public struct BoneEndpoints
    {
        [SerializeField] private Vector3 parent;
        [SerializeField] private Vector3 child;

        public Vector3 Parent
        {
            get { return parent; }
            set { parent = value; }
        }

        public Vector3 Child
        {
            get { return child; }
            set { child = value; }
        }
    }

}
