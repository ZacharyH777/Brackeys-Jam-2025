using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Data
{
    public enum SolveMode { TipFirst, RootFirst, Alternate }

    // Allows the solver to iterate FABRIK with different settings
    public struct DualEndSolverSettings
    {
        public SolveMode SolveMode;
        public int Iterations;
        public float Tolerance;
        public float Relaxation;
        
        public bool Planar2D;
        public bool AdaptivePassOrder;
        
        public float TargetSmoothing;
        
        // Controled by a pull object (maybe switch to a float)
        public float PullStrength;
        public float PullBias;
        public int PullPasses;
        
        // These are some jitter/smoothness checks and variables
        public float GlobalDamping;
        public bool BreakTwoCycle;
        public float TwoCycleEps2;
        public float TwoCycleMix;

        public float4x4 WorldToSolveSpace;
        public float4x4 SolveSpaceToWorld;
        
        public float3 AnchorALocal;
        public float3 AnchorBLocal;
        public float3 PullEndLocal;
    }

    public struct DualEndSolverState
    {
        public bool AnchorsInitialized;
        public float3 AnchorAPrevLocal;
        public float3 AnchorBPrevLocal;
    }
}
