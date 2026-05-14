using System;
using System.Collections.Generic;
namespace RunstarSystems.SystemAdmin
{
    // Allows a system to register before being sent to the GameAdmin
    public static class SystemRegistry
    {
        private static readonly List<IGameSystem> pendingSystems = new List<IGameSystem>();
        
        // Builds during the splash screen
        public static void Register(IGameSystem system)
        {
            pendingSystems.Add(system);
        }

        // Admin get registered systems
        public static List<IGameSystem> GetRegisteredSystems()
        {
            return pendingSystems;
        }
    }
}
