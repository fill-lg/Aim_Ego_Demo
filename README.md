# Aim_Ego Demo 🔫

A high-fidelity First-Person Shooter (FPS) controller prototype developed in Unity. This project focuses on achieving "snappy" and responsive movement and weapon handling, heavily inspired by the Source Engine (CS2).

## 🚀 Key Features

### 🏃 Movement System
- **Source-Style Physics**: Implementation of instant acceleration and deceleration on ground, and authentic **Air Strafing** mechanics.
- **Dynamic Height**: Full support for standing and crouching states with smooth transitions.
- **Fluidity**: High-precision movement calculations ensuring a competitive feel.

### 🎭 Animation System
- **Advanced Animator Controller**: A complex state machine managing transitions between different weapon types and movement states.
- **Blend Trees**: Used for seamless blending between Idle, Walk, and Run animations based on current velocity.
- **Humanoid Rigging**: Fully compatible with Humanoid avatars, ensuring smooth animation retargeting across different character models.
- **Weapon-Specific States**: Unique animation sets for Rifles, Pistols, and Melee weapons.

### 🔫 Weapon Mechanics
- **Slot-Based System**: Seamless switching between primary, secondary, and melee weapons.
- **ViewModel Integration**: Dedicated weapon view-models with procedural effects (recoil, muzzle flashes).
- **Item Interaction**: Implementation of a raycast-based weapon pickup and drop system.

## 🛠 Technical Stack
- **Engine**: Unity
- **Language**: C#
- **Version Control**: Git
- **Input**: Unity Input System (Action-based)

## 🎮 Controls
- **WASD**: Movement
- **Space**: Jump
- **Left Ctrl**: Crouch
- **Left Mouse**: Fire
- **R**: Reload
- **1, 2, 3**: Switch Weapon
- **E**: Pickup Weapon

## 📂 Project Architecture
The project follows a modular approach:
- `PlayerController.cs`: Handles physics and movement logic.
- `PlayerInputHandler.cs`: Centralized input processing.
- `PlayerAnimationController.cs`: Bridges physics state to the Animator.
- `PlayerShooting.cs`: Manages weapon logic, ammo, and combat.

---
*This project is developed as a technical demonstration of FPS movement and animation systems for a professional portfolio.*
