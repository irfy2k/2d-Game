# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project overview

Unity 2D action platformer prototype featuring a single-hit (1 HP) player/enemy combat loop, dash + parry mechanics, and an endless enemy spawning mode with score and local high score tracking.

Key technologies:
- Unity (2D, physics-based characters)
- New Input System (`UnityEngine.InputSystem`) for player controls
- TextMesh Pro (`TMPro`) for UI text

There is no `README.md` or C# solution/project file; the Unity Editor compiles and runs everything directly from `Assets/`.

## Common commands

> NOTE: Replace `<UNITY_EDITOR_PATH>` with your local Unity Editor executable and `<PROJECT_PATH>` with this repo path (e.g. `D:\Unity\Unity Projects\Testing`). Paths and exact CLI flags may vary slightly by Unity version.

### Open the project in Unity from PowerShell

```powershell
& "<UNITY_EDITOR_PATH>" -projectPath "<PROJECT_PATH>"
```

In practice, you’ll usually open the project via Unity Hub, but the above is useful for automation.

### Build a Windows player from the command line

```powershell
& "<UNITY_EDITOR_PATH>" `
  -projectPath "<PROJECT_PATH>" `
  -quit -batchmode `
  -buildWindows64Player "<PROJECT_PATH>\\Builds\\Game.exe" `
  -logFile "<PROJECT_PATH>\\Logs\\build.log"
```

Adjust output paths and build options as needed. This project does not define custom build scripts; builds are managed by Unity’s standard build pipeline.

### Run Unity tests (if/when they are added)

There are currently no Edit Mode or Play Mode test assemblies (`*Tests.cs`) in this repo. If you add tests using Unity’s Test Framework:

- From the Editor: open the **Test Runner** window and run Edit Mode / Play Mode tests as usual.
- From the command line, you can run all tests in a given platform:

```powershell
& "<UNITY_EDITOR_PATH>" `
  -projectPath "<PROJECT_PATH>" `
  -runTests -testPlatform playmode `
  -logFile "<PROJECT_PATH>\\Logs\\playmode-tests.log"
```

To run a single test by name:

```powershell
& "<UNITY_EDITOR_PATH>" `
  -projectPath "<PROJECT_PATH>" `
  -runTests -testPlatform playmode `
  -testNames "Namespace.ClassName.MethodName" `
  -logFile "<PROJECT_PATH>\\Logs\\single-test.log"
```

There are no repo-specific linting/formatting commands; rely on your C# IDE/Editor (Rider, VS, VS Code + analyzers) and Unity’s compiler warnings.

## High-level architecture

### Core systems and shared abstractions (`Assets/Scripts/CoreSystems.cs`)

- `IDamageable` interface: implemented by `PlayerController` and `EnemyController` so that hitboxes can interact with both using a common `TakeDamage(int)` API.
- `State` abstract base class: common template for game states, with `Enter`, `Exit`, `LogicUpdate`, and `PhysicsUpdate` methods and a `timeEntered` timestamp.
- `StateMachine` class: minimal state machine that owns a single `CurrentState`, calls `Enter` on initialization, and mediates `ChangeState` by calling `Exit` on the previous state then `Enter` on the new one.

This state machine is the backbone for both player and enemy behavior; new behavior is generally added by creating new `State` subclasses in the corresponding `*States.cs` file, then wiring them into the controller.

### Player architecture

**Files:**
- `Assets/Scripts/PlayerController.cs`
- `Assets/Scripts/PlayerStates.cs`

**PlayerController (`MonoBehaviour`, `IDamageable`)**

- Owns movement, dash, jump, attack combo tracking, parry, and dash-attack configuration via serialized fields (move speed, jump force, dash duration/cooldown, combo reset, ground detection, combat hitboxes, etc.).
- Uses the New Input System callbacks (`OnMove`, `OnJump`, `OnAttack`, `OnParry`, `OnDash`) to set transient input flags and directional input used by the state machine.
- Manages animation via pre-hashed animator state IDs (`AnimIdle`, `AnimRun`, `AnimJump`, etc.) and helper methods `PlayAnim(int)` / `PlayAnim(string)`.
- Provides physics helpers: `SetVelocityX`, `SetVelocityY`, `ResetGravity`, `IsGrounded` (via an overlap circle), and collision toggling with enemies (`SetEnemyCollision`).
- Tracks and exposes:
  - Dash and parry cooldowns (`CanDash`, `StartDashCooldown`, `CanParry`, `StartParryCooldown`).
  - Attack combos (`CheckCombo`, `GetComboCount`) with a 3-step combo and auto-reset.
  - Death state (`isDead`) and invulnerability (`isInvulnerable`) for dash / dash-attack.
- Implements `TakeDamage` with two overloads:
  - `TakeDamage(int damage, Collider2D damageSource)`: main entry used when a hit source is known (`DamageDealer` passes the hitting collider).
    - If already dead or currently invulnerable (e.g. dashing), ignore.
    - If currently in `PlayerParry` state, tries to resolve the attacker (`EnemyController` via the collider’s parent) and calls `OnParryStun` to stun the enemy instead of taking damage.
    - Otherwise, executes a one-hit death flow via `HandleDeath()`.
  - `TakeDamage(int damage)`: fallback when no source collider is known; routes to the above with `null`.
- Death flow:
  - `HandleDeath` sets `isDead`, stops movement, resets gravity, disables hitbox, then starts `DeathSequence`.
  - `DeathSequence` plays the DEATH animation for `deathAnimationTime` seconds, then invokes `GamePauseManager.Instance.HandleGameOver()` to show the game over UI and freeze time (or logs an error and sets `Time.timeScale = 0f` if the manager is missing).

**Player state machine (`PlayerStates.cs`)**

All player states subclass `State` and are constructed in `PlayerController.Awake` with a reference to the controller; `SM.Initialize(IdleState)` is called in `Start`.

- `PlayerIdle`:
  - On `Enter`: plays idle animation, zeroes horizontal velocity, resets gravity, disables the sword hitbox.
  - On `LogicUpdate`: decides transitions based on one-frame input flags and grounded state (dash, jump, attack, parry, move, or falling into `PlayerAir`).

- `PlayerMove`:
  - On `Enter`: plays run animation and resets gravity.
  - `LogicUpdate`: similar transition priorities (dash, jump, attack, parry, stop moving, or falling).
  - `PhysicsUpdate`: moves the player using `moveInput.x * moveSpeed`.

- `PlayerJump`:
  - On `Enter`: applies upward velocity (`jumpForce`), clears `jumpInput`, immediately transitions to `PlayerAir`.

- `PlayerAir`:
  - `LogicUpdate`: allows dash, air attack, or parry; returns to idle when grounded; otherwise swaps between jump/fall animation based on vertical velocity.
  - `PhysicsUpdate`: applies horizontal air control and variable gravity using `fallMultiplier`/`lowJumpMultiplier` and `isJumpPressed`.

- `PlayerAttack`:
  - On `Enter`: increments/rolls combo using `CheckCombo`, plays the appropriate attack animation (1–3), clears `attackInput`, and starts a timer.
  - `LogicUpdate`: tracks additional attack input to chain combos; at the end of `attackDuration`, either loops into another `PlayerAttack` (if `_comboTriggered`) or returns to `Idle`/`Air` depending on grounded state. Dash and grounded jump can interrupt.
  - `PhysicsUpdate`: horizontal movement during attacks.

- `PlayerParry`:
  - On `Enter`: plays parry animation, clears `parryInput`, restarts the local timer.
  - `LogicUpdate`: after `parryDuration`, starts parry cooldown, then transitions to `Idle` or `Air`. Dash and grounded jump can interrupt.
  - `PhysicsUpdate`: allows horizontal motion, with slightly reduced gravity while airborne for better feel.

- `PlayerDash`:
  - On `Enter`: plays dash animation, clears `dashInput`, starts dash cooldown, sets direction from input or facing, zeroes gravity, enables `isInvulnerable`, and disables physics collisions with enemies via `SetEnemyCollision(false)`.
  - On `Exit`: restores enemy collisions and clears invulnerability.
  - `LogicUpdate`: transitions into `PlayerDashAttack` if attack input occurs during dash, or back to `Idle`/`Air` after `dashDuration`.
  - `PhysicsUpdate`: drives high-speed horizontal movement and clamps vertical velocity to zero.

- `PlayerDashAttack`:
  - On `Enter`: plays dash-attack animation, disables gravity, sets `isInvulnerable`, clears `attackInput`.
  - On `Exit`: clears invulnerability.
  - `LogicUpdate`: after `attackDuration`, returns to `Idle` or `Air` and restores gravity.
  - `PhysicsUpdate`: moves horizontally at half dash speed in the facing direction.

**Extending player behavior**

- To add a new player state (e.g. `PlayerWallSlide`):
  - Implement a new `State` subclass in `PlayerStates.cs` that holds a reference to `PlayerController`.
  - Instantiate it in `PlayerController.Awake` and expose a field (e.g. `public PlayerWallSlide WallSlideState;`).
  - Add appropriate transitions in existing states’ `LogicUpdate` methods.

### Enemy architecture

**Files:**
- `Assets/Scripts/EnemyController.cs`
- `Assets/Scripts/EnemyStates.cs`

**EnemyController (`MonoBehaviour`, `IDamageable`)**

- Serialized stats: `speed`, `detectionRange`, `attackRange`, `health` (1 HP rule), `knockbackForce`.
- Timing: `stunDuration`, `attackWindup`, `attackCooldown`, `deathAnimationTime` for telegraphing, recovery, and death.
- References: `Rigidbody2D`, `Animator`, `Transform player`, attack hitbox `Collider2D`, `SpriteRenderer`, and `PlayerController`.
- Animation state hashes: `AnimIdle`, `AnimRun`, `AnimAttack`, `AnimHurt`, `AnimDeath`.
- State machine: instances of `EnemyIdle`, `EnemyChase`, `EnemyAttack`, `EnemyStun`, `EnemyDead`.
- On `Start`:
  - Finds the player via tag if not assigned.
  - Caches `PlayerController` and globally ignores solid-body collisions between all non-trigger colliders on enemy and player, so only hitbox triggers drive combat (prevents pushing-by-body).
  - Initializes the state machine in `Idle`.

- Implements `IDamageable.TakeDamage`:
  - Ignores damage when already in `EnemyDead`.
  - Decrements `health` and, when it reaches 0, stops coroutines and transitions to `DeadState`.

- Provides stun hook:
  - `OnParryStun` is called by `PlayerController` during a successful parry; it stops attack coroutines and moves into `EnemyStun` if not already stunned or dead.

- Knockback and helpers:
  - `ApplyKnockback` makes the body dynamic and sets a strong velocity away from facing direction.
  - Movement helpers: `MoveTowardsPlayer`, `StopMovement`.
  - Distance helpers: `GetDistanceToPlayer`, `GetSqrDistanceToPlayer` (returns `float.MaxValue` if no player or player is dead), and squared range properties.
  - Coroutine wrapper `StartTrackedCoroutine` tracks and cancels ongoing behaviors safely when switching states.

**Enemy states (`EnemyStates.cs`)**

- Base `EnemyState` holds `_ec` reference and derives from `State`.

- `EnemyIdle`:
  - On `Enter`: sets body type to `Kinematic` (prevents drag/jitter), plays idle animation, stops movement.
  - `LogicUpdate`: if player is dead, remains idle; otherwise transitions to `Chase` when within detection range.

- `EnemyChase`:
  - On `Enter`: sets body to `Dynamic`, plays run animation.
  - `LogicUpdate`:
    - If the player is dead: stop and go back to idle.
    - If player leaves detection range: back to idle.
    - Special handling: if player is in a dash/dash-attack state and moving toward the enemy within an extended range, the enemy may pre-emptively transition to `Attack` as a counter.
    - When within attack range: transitions to `Attack`.
  - `PhysicsUpdate`: continues moving toward player’s X position while they’re alive.

- `EnemyAttack`:
  - On `Enter`: if player is already dead, returns to idle; otherwise zeroes movement and starts `AttackSequence`.
  - `PhysicsUpdate`: constantly zeros horizontal movement so the enemy remains planted during the animation.
  - `Exit`: disables hitbox and stops movement.
  - `AttackSequence` coroutine:
    - Telegraph: plays idle, tints `SpriteRenderer` yellow, waits for `attackWindup`.
    - Attack: plays attack animation (hitbox enabled via animation events).
    - Cooldown: waits `attackCooldown`.
    - If player died during the sequence: disables hitbox and idles.
    - Otherwise, if player remains near (`attackRange` + buffer) re-enters `Attack`; else returns to `Chase`.

- `EnemyStun`:
  - On `Enter`: plays hurt animation, stops movement, sets body to `Kinematic`, tints sprite blue, starts `StunSequence`.
  - `Exit`: returns sprite color to white, body to `Dynamic`.
  - `StunSequence`: waits `stunDuration` then transitions to `Chase`.

- `EnemyDead`:
  - On `Enter`: ensures `Dynamic` body, disables attack hitbox, applies knockback, plays death animation, starts `DeathSequence`.
  - `DeathSequence`: waits `deathAnimationTime`, zeroes velocity, invokes `EnemyController.OnEnemyKilled(this)` so `EndlessGameManager` can update score and spawn logic, then destroys the enemy GameObject.

**Extending enemy behavior**

- To add new behaviors (e.g. ranged attack, patrol): create new `EnemyState` subclasses or expand existing ones, then wire them into `EnemyController` and state transitions in `EnemyStates.cs`. Maintain the pattern of using `StartTrackedCoroutine` for timed behaviors.

### Combat and hitbox system

**Files:**
- `Assets/Scripts/DamageDealer.cs`
- `Assets/Scripts/DrawHitbox.cs`

**DamageDealer**

- Intended to be attached to trigger colliders used as weapon or hazard hitboxes.
- Uses a `LayerMask targetLayer` to filter collisions.
- On trigger enter:
  - Confirms collided object is on a target layer.
  - Gets `IDamageable` via `GetComponentInParent<IDamageable>` to support hitboxes that are child objects of the actual entity.
  - If the target is a `PlayerController`, calls `TakeDamage(damageAmount, sourceCollider)` so the player can derive the attacking `EnemyController` for parry stun.
  - Otherwise, calls `target.TakeDamage(damageAmount)` directly (used for enemies).

**DrawHitbox**

- Utility for visualizing hitboxes in the Scene view using `OnDrawGizmos`.
- Reads `BoxCollider2D` size/offset and draws a filled semi-transparent box and a red wireframe outline.

### Game flow & UI controllers

**Files:**
- `Assets/Scripts/GamePauseManager.cs`
- `Assets/Scripts/MainMenuController.cs`
- `Assets/Scripts/EndlessGameManager.cs`

**GamePauseManager**

- Singleton (`Instance`) used by `PlayerController` and other systems.
- Manages:
  - `pauseUI` and `gameOverUI` roots.
  - Global pause state (`IsPaused`) and `Time.timeScale`.
- Input:
  - `OnPause(InputAction.CallbackContext)` is wired via the new Input System and toggles pause when the action is performed and the game over UI is not active.
- Methods:
  - `TogglePause(bool shouldPause)`: shows/hides pause UI and sets `Time.timeScale` unless a game over UI is active.
  - `HandleGameOver()`: sets paused, asks `EndlessGameManager` to process final scores (`OnGameOver()`), shows game over UI, freezes time.
  - `OnResumeButton()`, `RestartLevel()`, `ReturnToMenu()`, `ExitGame()`: intended for UI button hooks; manage scene reload or exit and ensure `Time.timeScale` is restored to 1 before scene transitions.

**MainMenuController**

- Controls the main menu scene (likely `MainMenu.unity`).
- Ensures `Time.timeScale = 1f` in `Start` so entering the menu from a paused/game-over state unfreezes time.
- `StartGame()`: sets `Time.timeScale = 1f` and loads the configured `gameSceneName` (must match a scene in Build Settings).
- `QuitGame()`: calls `Application.Quit()`; in the editor, also stops play mode via `UnityEditor.EditorApplication.isPlaying = false` inside `#if UNITY_EDITOR`.

**EndlessGameManager**

- Singleton-like manager (`Instance`) for endless mode enemy spawning and scoring.
- Expects an `EnemyController` prefab and one or more spawn points.
- On `Awake`:
  - Enforces singleton, caches base enemy stats (speed, detection range, attack cooldown) from the prefab for scaling, reads high score from `PlayerPrefs`, and updates UI.
- On `OnEnable`/`OnDisable`:
  - Subscribes to `EnemyController.OnEnemyKilled` to update score and active enemy count.
- On `Start`:
  - Validates `enemyPrefab` and schedules the first spawn after `initialSpawnDelay`.
- On `Update`:
  - If not game over, tracks elapsed time and spawns enemies at decreasing intervals while the active enemy count is below a time-scaled maximum (`GetDesiredMaxEnemies`).
- `SpawnEnemy()`:
  - Instantiates an enemy at a random spawn point (or manager position fallback).
  - Increments `_activeEnemies` and scales enemy speed/detection range based on elapsed time versus configured ramp durations.
  - Maintains the 1 HP rule and keeps attack cadence stable by applying multipliers only where intended.
- Score/high-score handling:
  - `_score` increments on each enemy kill; UI is updated via `UpdateScoreUI`.
  - `OnGameOver()` locks further scoring/spawning, persists new high score to `PlayerPrefs` under `highScoreKey`, and refreshes gameplay and game-over UI labels.

### Scenes and third-party content

- Core scenes are under `Assets/Scenes/` (e.g. `MainMenu.unity`, `GameScene.unity`), with additional demo scenes for imported assets under vendor-specific folders.
- Third-party content:
  - **Cainos Pixel Art Platformer - Village Props**: visuals and example scripts for environment and moving platforms.
  - **Lucid Editor** under `Assets/Third Party/Lucid Editor/`: editor/runtime attributes and custom inspector tooling.
  - **TextMesh Pro examples** under `Assets/TextMesh Pro/Examples & Extras/`.

These third-party folders are primarily vendor code and examples; avoid modifying them unless you are intentionally extending or debugging those packages.
