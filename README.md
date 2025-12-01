<img width="1430" height="779" alt="Screenshot from 2025-12-01 13-58-49" src="https://github.com/user-attachments/assets/6e0d749c-7f89-4e16-b2c0-cea48eb59fff" />
<img width="865" height="514" alt="Screenshot from 2025-12-01 13-59-10" src="https://github.com/user-attachments/assets/20fd0c76-a983-45d4-a356-f3099371dd3a" />
# 🎮⚔️ **Riposte — A Fast-Paced 2D Parry Combat Game**

*A Unity 2D Action Mini-Game Built for Quick 5–10 Minute Fun Sessions*

---

## 🌟 **Overview**

**Riposte** is a lightweight, fast-paced 2D Unity game crafted purely for **entertainment** — ideal for casual gamers who want a short, skill-based play session.

Featuring **tight controls**, **instant feedback mechanics**, and **simple but rewarding combat**, Riposte challenges players to dodge, slash, and **time perfect parries** against swarming goblins.

---

## 📸 **Screenshots**

> *(Add your images here — recommended size: 800px wide)*
> Example placeholders:

| Main Menu                       | Gameplay                                | Parry Effect                      |
| ------------------------------- | --------------------------------------- | --------------------------------- |
| ![Menu](./screenshots/menu.png) | ![Gameplay](./screenshots/gameplay.png) | ![Parry](./screenshots/parry.png) |

---

# 🕹️ **Gameplay Features**

### 🏠 **Main Menu**

* Clean, minimal, quick-to-navigate interface
* Start, Quit options
* Smooth transitions

---

### 🏃‍♂️💨 **Core Mechanics: Movement, Jump, Dash, Attack**

* Fluid 2D platformer movement
* Lightning-fast dash
* Responsive melee attack
* Endless mode for continuous action

---

### 🛡️✨ **Parry System**

* Time your parry against goblin attacks
* Successful parry stuns enemies (blue flash effect)
* Stunned goblins cannot attack for a duration
* Rewarding and skill-based

---

### 👺🔥 **Scaling Difficulty**

* Goblin count increases as time passes
* Death animation, restart option
* Score + Highscore tracking (PlayerPrefs)

---

# 🧱 **Technologies & Tools**

### 💻 Language & Engine

* **C#**
* **Unity 6.0 LTS** (2D URP)

### 🧩 Frameworks & APIs

* Unity **Input System**
* Unity **Animator** (animation states)
* Unity **Physics2D**
* TextMesh Pro

### 🛠 Development Tools

* Visual Studio / VS Code
* Git + GitHub
* Unity Package Manager

### 🎨 Assets

* Pixel art sprites + animations
* Imported from Unity Asset Store

---

# 🏗 **Architecture Overview**

Riposte is structured using a clean **layered architecture** to ensure scalability and maintainability.

```
┌──────────────────────────────┐
│            UI Layer           │
│──────────────────────────────│
│ Menus, HUD, Pause, GameOver   │
│ MainMenuController, HUD, TMP   │
└──────────────┬───────────────┘
               │
┌──────────────┴───────────────┐
│      Business Logic Layer     │
│──────────────────────────────│
│ PlayerController, EnemyAI     │
│ State Machines (Player, AI)   │
│ Combat, Game Managers         │
└──────────────┬───────────────┘
               │
┌──────────────┴───────────────┐
│     Data Access Layer (DAL)   │
│──────────────────────────────│
│ PlayerPrefs (High Score)      │
│ Local key-value persistence   │
└───────────────────────────────┘
```

---

# 🧠 **OOP Concepts Applied**

### 🔷 **Abstraction + Polymorphism**

* `IDamageable` interface
* Implemented by Player & Enemy

### 🔷 **Inheritance**

* Hierarchy of state classes (idle, move, jump, dash, attack…)

### 🔷 **Encapsulation**

* Properties like `canDash` hide internal dash logic

---

# 🧩 **Design Patterns**

### 🎭 **Primary Pattern: State Pattern**

Used for both **Player** and **Enemy** behaviors

* Player states: *idle, move, jump, dash, attack, parry*
* Enemy states: *idle, chase, attack, stun, dead*

**Benefits:**

* No giant Update() conditions
* Behavior cleanly separated
* Easy to add new states

---

### 🟦 **Secondary Pattern: Singleton**

Used in:

* `GamePauseManager`
* `EndlessGameManager`

Centralizes game-wide functions like pausing and score handling.

---

# 🗄️ **Database Structure (PlayerPrefs)**

Riposte uses **Unity PlayerPrefs** for lightweight, cross-platform local storage.

| Key                  | Description          |
| -------------------- | -------------------- |
| `"EndlessHighScore"` | Stores highest score |

### Operations:

```csharp
PlayerPrefs.SetInt("EndlessHighScore", highScore);
PlayerPrefs.Save();
```

```csharp
int hs = PlayerPrefs.GetInt("EndlessHighScore", 0);
```

### Why PlayerPrefs?

* Cross-platform
* No external DB
* Perfect for small games
* Automatically stored per OS

---

# 🔁 **Game Loop — The Engine Behind the Game**

Every Unity game follows the same rhythm:

```
INPUT → UPDATE (Simulation) → RENDER
```

### 🎮 Responsiveness

Instant reaction to player input.

### 🧮 Consistency

Stable behavior across devices.

### 🧩 Built-in Callbacks

* `Update()` for logic
* `FixedUpdate()` for physics
* `LateUpdate()` for camera + cleanup

---

# ⏱ **Loop Mathematics**

### `Time.deltaTime`

Ensures **frame-rate independent movement**

```
position += velocity * Time.deltaTime;
```

### Physics in `FixedUpdate()`

Stable, deterministic collisions and gravity.

---

# 🔧 Simulation (Update Phase)

This is where the game thinks:

* Player controls
* Enemy AI behavior
* Collision checks
* Physics logic
* State transitions

Avoid placing rendering code here.

---

# 🎨 Rendering (Draw Phase)

Unity handles rendering automatically after simulation:

* Camera
* SpriteRenderer
* URP, lighting, sorting layers
* Purely visual — **no logic here**

---

# 🎯 Why This Matters

Proper separation ensures:

* Smooth movement
* Predictable physics
* Zero jitter
* Maintainable code
* Better optimization (CPU vs GPU load split)

---


# 📦 **How to Run the Project**

1. Clone the repository
2. Open project in **Unity 6.0 LTS**
3. Open `MainMenu` scene
4. Press ▶ Play

---

# 🎉 Thank You for Playing Riposte!

If you enjoy the game, give the repo a ⭐ star and share your feedback!

