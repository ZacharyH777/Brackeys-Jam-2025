namespace RunstarSystems.SystemAdmin
{
    // Required interface for the game system to work
    // @TODO: Not quite sure how to manage scope in this yet
    public interface IGameSystem
    {
        void Setup();
        void Tick(float deltaTime);
        // You can add priority to control the execution order
        int ExecutionPriority { get; }
    }
}
