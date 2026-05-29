namespace RunstarSystems.IKSystem
{
    class IKSystem : SystemAdmin.IGameSystem
    {
        public int ExecutionPriority { get; private set; } = 50;
        public void Setup()
        {
            return;
        }

       public  void Tick(float deltaTime)
        {
            return;
        } 
    } 
}
