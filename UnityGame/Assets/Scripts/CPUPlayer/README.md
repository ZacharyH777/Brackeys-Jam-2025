# CPU Player System

A comprehensive AI system for creating intelligent computer-controlled opponents in the Unity ping pong game. This system provides automatic serving, ball tracking, movement, and difficulty-based behavior.

## Quick Start

### Automatic Setup (Recommended)

1. Add `CPUOneClickSetup` component to any GameObject in your scene
2. Set `autoSetupOnPlay = true` (default)
3. Press Play - CPU support is configured automatically
4. The system will convert P2 to CPU in single-player mode

### Manual Setup

1. Add `CPUPlayer` component to your player GameObject
2. Add `CPUMovement` component for physics-based movement
3. Add `PlayerOwner` component and set `player_id` to P2
4. Assign ball reference in `CPUPlayer.ball` field

## Core Components

### CPUPlayer

Main AI controller that handles decision-making and ball tracking.

```csharp
public class CPUPlayer : MonoBehaviour
{
    public BallPhysics2D ball;           // Ball to track
    public PlayerOwner player_owner;     // Player identification
    public CPUMovement cpu_movement;     // Movement controller
    public CPUDifficulty difficulty;     // AI difficulty level
}
```

**Key Features:**

- Ball trajectory prediction
- Automatic serving with human-like delays
- Stats-based behavior system
- Real-time ball tracking and interception

### CPUMovement

Physics-based movement system for CPU players.

```csharp
public class CPUMovement : MonoBehaviour
{
    public float max_speed = 12f;        // Maximum movement speed
    public float acceleration = 60f;     // Acceleration rate
    public float movement_smoothing = 0.8f; // Movement smoothing
}
```

**Features:**

- Smooth rigidbody-based movement
- Target position tracking
- Configurable acceleration and deceleration
- Integration with animation systems

### StatSystem

Flexible stats management for CPU behavior customization.

```csharp
// Example usage
statSystem.SetStatValue("ReactionTime", 0.25f);
statSystem.SetStatValue("MovementAccuracy", 0.8f);
```

## Difficulty System

### Built-in Difficulties

- **Easy**: Slower reactions, less accurate (Reaction: 0.4s, Accuracy: 60%)
- **Medium**: Balanced performance (Reaction: 0.25s, Accuracy: 80%)
- **Hard**: Fast and precise (Reaction: 0.15s, Accuracy: 95%)

### AI Profiles

Advanced personality system with predefined behaviors:

- **Beginner Bot**: Forgiving AI perfect for learning
- **Balanced Player**: Even match for most skill levels
- **Pro Champion**: Maximum difficulty challenge
- **The Wall**: Defensive specialist focusing on consistency
- **Power Hitter**: Aggressive attacker taking risks
- **Learning AI**: Adaptive behavior that adjusts to player skill

## Stats System

### Core AI Stats

| Stat Name           | Description                        | Default Range |
| ------------------- | ---------------------------------- | ------------- |
| `ReactionTime`      | Base reaction delay (seconds)      | 0.1 - 2.0     |
| `MovementAccuracy`  | How precisely AI targets positions | 0.0 - 1.0     |
| `PredictionTime`    | Ball trajectory lookahead time     | 0.1 - 2.0     |
| `ServeDelayMin/Max` | Serving delay range                | 0.5 - 10.0    |
| `ReachableDistance` | Maximum ball chase distance        | 1.0 - 10.0    |
| `Consistency`       | Overall AI performance stability   | 0.0 - 1.0     |

### Advanced Stats

- `AggressionLevel`: Risk-taking behavior
- `PatternRecognition`: Ability to learn player patterns
- `AdaptiveDifficulty`: Dynamic difficulty adjustment
- `OpponentPredictionAccuracy`: Player behavior prediction

## Architecture

### Decision Making Flow

1. **Ball Tracking**: Continuous position and velocity monitoring
2. **Prediction**: Calculate ball trajectory using physics
3. **Decision**: Determine target position based on game state
4. **Movement**: Send target to CPUMovement component
5. **Execution**: Physics-based movement to intercept ball

### Serving System

```csharp
// CPU automatically serves when it's their turn
public void RequestServe()
{
    float delay = Random.Range(serveDelayMin, serveDelayMax);
    // Schedule serve after human-like delay
}
```

### Ball Hitting Requirements

CPU players need these components to hit the ball:

- `PaddleKinematics`: Calculates paddle velocity for realistic physics
- `SurfaceZone` (Paddle type): Collision detection zones
- Properly configured rigidbody and colliders

## Configuration

### Stat Presets

Create custom AI behaviors using `StatPreset` ScriptableObjects:

```csharp
var preset = ScriptableObject.CreateInstance<StatPreset>();
preset.stats.Add(new StatPreset.PresetStat {
    name = "ReactionTime",
    value = 0.3f,
    minValue = 0.1f,
    maxValue = 2.0f
});
```

### Runtime Modification

```csharp
// Change difficulty mid-game
cpuPlayer.difficulty = CPUDifficulty.Hard;

// Apply custom stats
cpuPlayer.ApplyCustomPreset(myStatPreset);

// Direct stat modification
cpuPlayer.GetComponent<StatSystem>().SetStatValue("ReactionTime", 0.2f);
```

## Integration

### Single Player Mode

```csharp
CharacterSelect.is_singleplayer = true;
CharacterSelect.p2_is_cpu = true;
CharacterSelect.cpu_difficulty = CPUDifficulty.Medium;
```

### Multiplayer Integration

- CPU players can replace human players dynamically
- Supports multiple CPU players in the same match
- Automatic device management and input cleanup

### Game Loop Integration

The CPU system integrates with:

- `PingPongLoop`: Serves automatically when notified
- `BallPhysics2D`: Tracks ball for decision making
- `FixedSlotSpawner`: Spawns CPU players in appropriate slots

## Testing & Debugging

### Diagnostics

The `CPUOneClickSetup` provides comprehensive diagnostics:

```
🏓 Testing CPU serving connection...
✓ CPU Player 'BodyDown(Clone)' with PlayerOwner ID: P2
🎯 CPU Player 'BodyDown(Clone)' hitting components: PaddleKinematics=True, Paddle Zones=1
```

### Common Debug Messages

- `"CPU Player P2 will serve in 1.42s"` - Serving scheduled
- `"CPU Player P2 executing serve now!"` - Serve executed
- `"Applied AI profile: Balanced Player"` - Profile loaded
- `"🎯 CPU Player P2 hitting components: PaddleKinematics=True"` - Component check

### Performance Monitoring

- Ball tracking updates every frame
- Decision making runs at reaction intervals
- Movement commands sent to physics system
- Stat changes cached for performance

## Troubleshooting

### CPU Not Serving

- Ensure `PlayerOwner` component exists with correct `player_id`
- Check `PingPongLoop` is calling `NotifyCPUPlayerIfNeeded()`
- Verify CPU player has `IsCPUPlayer` returning true

### CPU Not Moving

- Check `CPUMovement` component is attached and enabled
- Verify ball reference is assigned in `CPUPlayer.ball`
- Ensure rigidbody is Dynamic and simulated

### CPU Not Hitting Ball

- Verify `PaddleKinematics` component exists in hierarchy
- Check for `SurfaceZone` components with `ZoneType.Paddle`
- Ensure colliders are properly configured as triggers

### Performance Issues

- Reduce `PredictionTime` for less lookahead calculation
- Lower `MovementAccuracy` to reduce precision calculations
- Disable verbose logging in production builds

## File Structure

```
CPUPlayer/
├── Core/
│   ├── CPUPlayer.cs                    # Main AI controller
│   ├── CPUMovement.cs                  # Physics-based movement
│   ├── CPUOneClickSetup.cs             # Automatic scene setup
│   └── AIProfileDatabaseCreator.cs     # Profile database creation
├── Stats/
│   ├── StatSystem.cs                   # Flexible stats management
│   ├── StatPreset.cs                   # ScriptableObject for stat configs
│   ├── AIStatPresets.cs                # Predefined stat configurations
│   ├── AIProfileManager.cs             # Profile switching and management
│   └── RuntimeAIStatModifier.cs        # UI for real-time stat adjustment
├── Tools/
│   ├── CPUPrefabGenerator.cs           # Utility for creating CPU variants
│   └── CPUPlayerTester.cs              # Test harness for CPU functionality
└── Editor/
    └── AIProfileDatabaseGenerator.cs   # Editor tools for database creation
```

## API Reference

### CPUPlayer Methods

- `RequestServe()`: Schedule CPU serve with delay
- `ApplyCustomPreset(StatPreset preset)`: Apply stat configuration
- `bool IsCPUPlayer`: Always returns true for identification

### CPUMovement Methods

- `SetTargetPosition(Vector2 target)`: Set movement target
- `SetMovementSmoothing(float smoothing)`: Adjust movement smoothing

### StatSystem Methods

- `SetStatValue(string name, float value)`: Set stat value
- `GetStatValue(string name)`: Get current stat value
- `AddModifier(string stat, float value, type, priority, source)`: Add temporary modifier

## Dependencies

- Unity Input System (for input handling)
- Unity Physics 2D (for movement and collision)
- Game-specific components: `PingPongLoop`, `BallPhysics2D`, `PlayerOwner`
