# 🎮 CPU Player System - Quick Start Guide

## 🚀 Super Easy Setup (30 seconds)

### Step 1: Add CPU Support to Your Scene
1. Open your Arcade scene
2. Create an empty GameObject and name it "CPU Setup"
3. Add the `CPUOneClickSetup` component to it
4. Click "🚀 Setup CPU Support" in the inspector
5. Done! Your scene now supports CPU players.

### Step 2: Test It
1. Click "🧪 Test CPU Player" to verify everything works
2. Press Play and enjoy single player mode against AI!

---

## 📁 Folder Structure

```
CPUPlayer/
├── Core/           # Essential components
│   ├── CPUPlayer.cs        # Main CPU AI logic
│   ├── CPUMovement.cs      # CPU movement behavior  
│   └── CPUOneClickSetup.cs # One-click scene setup
├── Stats/          # AI intelligence system
│   ├── StatSystem.cs       # Flexible stats framework
│   ├── StatPreset.cs       # Preset configurations
│   ├── AIStatPresets.cs    # Pre-made AI personalities
│   └── AIProfileManager.cs # Profile management
├── UI/             # User interface components
│   ├── RuntimeAIStatModifier.cs  # Real-time AI tuning
│   └── CPUDifficultySelector.cs  # Difficulty picker
└── Tools/          # Development utilities
    ├── CPUPrefabGenerator.cs # Auto-generate CPU prefabs
    └── CPUDebugger.cs        # Debug and test AI
```

---

## 🎯 Quick Commands

### CPUOneClickSetup Component
- **🚀 Setup CPU Support**: One-click scene configuration
- **🧪 Test CPU Player**: Verify CPU functionality
- **🔄 Reset CPU Setup**: Clear configuration

### CPUDebugger Component (optional)
- **Press F1**: Toggle real-time debug display
- **🔄 Cycle CPU Difficulty**: Test different AI levels
- **🎲 Apply Random Preset**: Try different AI personalities
- **📊 Log CPU State**: Detailed console information

---

## ⚙️ How It Works

1. **CPUOneClickSetup** automatically:
   - Finds your scene's spawners and game controller
   - Configures character prefabs for CPU support
   - Sets up the AI profile system
   - Enables single player mode

2. **CPU Players** use a **stats-based AI system**:
   - Reaction time, accuracy, prediction, etc.
   - Easy/Medium/Hard difficulties built-in
   - Special personalities (Aggressive, Defensive, etc.)

3. **Everything is modular**:
   - Use just the core components if you want simple AI
   - Add UI components for player customization
   - Include debug tools for development

---

## 🔧 Advanced Usage

### Custom AI Personalities
```csharp
// Create your own AI preset
var customPreset = new StatPreset {
    presetName = "Lightning Fast",
    description = "Ultra-quick reactions"
};

// Apply to any CPU player
cpuPlayer.ApplyCustomPreset(customPreset);
```

### Runtime Stat Modification
```csharp
// Make CPU faster temporarily
cpuPlayer.AddStatModifier(AIStatNames.REACTION_TIME, -0.2f, 
                         StatModifierType.FlatAddition, 100, this);
```

---

## 🐛 Troubleshooting

**"CPU setup failed"**: Make sure your scene has `FixedSlotSpawner` components with P1/P2 slots and character prefabs assigned.

**"No CPU Player found"**: The CPU is only spawned when `CharacterSelect.p2_is_cpu = true` and the scene spawns players.

**"CPU isn't moving"**: Check that the ball physics component exists and the CPU can track it.

---

## 🎊 That's It!

Your ping pong game now has intelligent CPU opponents that can play at different skill levels. The system is designed to be simple to integrate but powerful enough for advanced customization.

Happy coding! 🏓