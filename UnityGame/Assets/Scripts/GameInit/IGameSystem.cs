namespace RunstarSystems
{
    public interface IGameSystem
    {
        void Setup();
        void Tick(float deltaTime);
        // You can add priority to control the execution order
        int ExecutionPriority { get; }
    }
}
