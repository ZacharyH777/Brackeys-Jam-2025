using UnityEngine;
using System.Collections;
using Admin = RunstarSystems.SystemAdmin;

namespace RunstarSystems.IKSystem
{
    public class IKSystem : IGameSystem
    {
        // Look at Scripts/GameInit/AdminSystem/GameAdmin.cs
        // for more details
        public int ExecutionPriority => 50;

        // Before Admin runs
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Subscribe()
        {
            Admin.SystemRegistry.Register(new IKSystem());
        }

        // Lets the system setup allocators and such.
        public void Setup()
        {
            ;
        }
        public void Tick(float dt)
        {
            
        }
    }
}
