using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RunstarSystems.SystemAdmin
{
    public class GameAdmin : MonoBehaviour
    {
        private IGameSystem[] fixedSystems; // Priorities 1-10 
        private IGameSystem[] updateSystems; // Priorities 11-30
        private IGameSystem[] lateSystems;   // Priorities 31+

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameObject go = new GameObject("GameAdmin");
            Object.DontDestroyOnLoad(go);
            var admin = go.AddComponent<GameAdmin>();

            var systems = SystemRegistry.GetRegisteredSystems();
            admin.Initialize(systems);
        }

        public void Initialize(List<IGameSystem> systems)
        {
            // Sort all systems by priority first
            var sorted = systems.OrderBy(system => system.ExecutionPriority).ToList();

            // Categorize based on your priority brackets
            fixedSystems = sorted.Where(system => system.ExecutionPriority <= 10).ToArray();
            updateSystems = sorted.Where(system => system.ExecutionPriority > 10 && system.ExecutionPriority <= 30).ToArray();
            lateSystems = sorted.Where(system => system.ExecutionPriority > 30).ToArray();

            // Run Setup on everything
            foreach (var system in sorted)
                system.Setup();
        }

        // Runs at the Physics rate
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < fixedSystems.Length; i++)
            {
                fixedSystems[i].Tick(dt);
            }
        }

        // Runs once per frame. Good for general Game Rules and Input
        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < updateSystems.Length; i++)
            {
                updateSystems[i].Tick(dt);
            }
        }

        // Runs after Update.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < lateSystems.Length; i++)
            {
                lateSystems[i].Tick(dt);
            }
        }
    }
}
