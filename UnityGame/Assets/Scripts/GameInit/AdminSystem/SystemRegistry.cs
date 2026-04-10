using System;
using System.Collections.Generic;
namespace RunstarSystems.SystemAdmin
{
    public static class SystemRegistry
    {
        private static readonly List<IGameSystem> pendingSystems = new List<IGameSystem>();
        
        // Systems call this during BeforeSplashScreen
        public static void Register(IGameSystem system)
        {
            pendingSystems.Add(system);
        }

        // Admin calls this during BeforeSceneLoad
        public static List<IGameSystem> GetRegisteredSystems()
        {
            return pendingSystems;
        }
    }
}
