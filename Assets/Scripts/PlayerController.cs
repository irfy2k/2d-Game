using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 13f;

    [Header("Dash Settings")]
    public float dashSpeed = 26f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.6f;
    private float _nextDashTime = 0f;

    [Header("Jump Physics")]
    public float fallMultiplier = 4f;
    public float lowJumpMultiplier = 2.5f;

    [Header("Combat Settings")]
    public float attackDuration = 0.3f;
    public float parryDuration = 0.25f;
    public float parryCooldown = 0.35f;
    private float _nextParryTime = 0f;

    [Header("Combo Settings")]
    public float comboResetTime = 0.5f;
    private int _comboCount = 0;
    private float _lastAttackTime = 0f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Combat References")]
    public Transform attackPoint;
    public float attackRange = 0.5f;
    public LayerMask enemyLayer;
    public Collider2D swordCollider;

    [Header("References")]
    public Rigidbody2D rb;
    public Animator anim;
    public SpriteRenderer sr;

    // Animation state hashes (avoid string lookups each play).
    public static readonly int AnimIdle = Animator.StringToHash("IDLE");
    public static readonly int AnimRun = Animator.StringToHash("RUN");
    public static readonly int AnimJump = Animator.StringToHash("JUMP");
    public static readonly int AnimFall = Animator.StringToHash("FALL");
    public static readonly int AnimAttack1 = Animator.StringToHash("ATTACK");
    public static readonly int AnimAttack2 = Animator.StringToHash("ATTACK2");
    public static readonly int AnimAttack3 = Animator.StringToHash("ATTACK3");
    public static readonly int AnimParry = Animator.StringToHash("PARRY");
    public static readonly int AnimDash = Animator.StringToHash("DASH");
    public static readonly int AnimDashAttack = Animator.StringToHash("DASH_ATTACK");
    public static readonly int AnimDeath = Animator.StringToHash("DEATH");

    [Header("Death/UI")]
    public bool isDead = false;              // Tracks if the knight is dead
    public float deathAnimationTime = 0.8f; // How long to play the DEATH animation
    // REMOVED: public GameObject gameOverUI; // Removed redundancy

    // --- Invulnerability ---
    public bool isInvulnerable = false; // Now public to be visible in Inspector

    // --- State Machine ---
    public StateMachine SM;
    public PlayerIdle IdleState;
    public PlayerMove MoveState;
    public PlayerJump JumpState;
    public PlayerAir AirState;
    public PlayerAttack AttackState;
    public PlayerParry ParryState;
    public PlayerDash DashState;
    public PlayerDashAttack DashAttackState;

    // --- Inputs ---
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public bool jumpInput;
    [HideInInspector] public bool isJumpPressed;
    [HideInInspector] public bool attackInput;
    [HideInInspector] public bool parryInput;
    [HideInInspector] public bool isParryHeld;
    [HideInInspector] public bool dashInput;

    // --- ANIMATION EVENTS ---
    public void EnableHitbox() { if (swordCollider != null) swordCollider.enabled = true; }
    public void DisableHitbox() { if (swordCollider != null) swordCollider.enabled = false; }

    // --- AUDIO ANIMATION EVENTS ---
    // These can be called from ATTACK / ATTACK2 / ATTACK3 animations.
    public void PlayAttackSwingSfx()
    {
        if (HitEffectsManager.Instance != null)
        {
            HitEffectsManager.Instance.PlayPlayerAttackSwing();
        }
    }

    // Can be called from the PARRY animation.
    public void PlayParrySfx()
    {
        if (HitEffectsManager.Instance != null)
        {
            HitEffectsManager.Instance.PlayParrySfx();
        }
    }

    private void Awake()
    {
        SM = new StateMachine();
        IdleState = new PlayerIdle(this);
        MoveState = new PlayerMove(this);
        JumpState = new PlayerJump(this);
        AirState = new PlayerAir(this);
        AttackState = new PlayerAttack(this);
        ParryState = new PlayerParry(this);
        DashState = new PlayerDash(this);
        DashAttackState = new PlayerDashAttack(this);
    }

    private void Start()
    {
        SM.Initialize(IdleState);
        DisableHitbox();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (isDead) return;
        SM.CurrentState.LogicUpdate();

        // Clear one-shot input flags so they are not buffered across states/frames.
        ClearInputFlags();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        SM.CurrentState.PhysicsUpdate();
    }

    /// <summary>
    /// Clears one-shot input flags (jump/attack/parry/dash) at the end of each frame.
    /// This prevents inputs pressed in the "wrong" state (e.g. mid-air jump press)
    /// from being consumed later when we return to Idle/Move.
    /// </summary>
    private void ClearInputFlags()
    {
        jumpInput = false;
        attackInput = false;
        parryInput = false;
        dashInput = false;
    }

    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) { jumpInput = true; isJumpPressed = true; }
        if (ctx.canceled) isJumpPressed = false;
    }

    public void OnAttack(InputAction.CallbackContext ctx) { if (ctx.performed) attackInput = true; }

    public void OnParry(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) { parryInput = true; isParryHeld = true; }
        if (ctx.canceled) isParryHeld = false;
    }

    public void OnDash(InputAction.CallbackContext ctx) { if (ctx.performed) dashInput = true; }

    // --- HELPER METHODS ---

    public void PlayAnim(int stateHash)
    {
        anim.Play(stateHash);
    }

    // Fallback overload if you still want to use raw string names somewhere.
    public void PlayAnim(string newState)
    {
        anim.Play(newState);
    }

    public void SetVelocityX(float x)
    {
        rb.linearVelocity = new Vector2(x, rb.linearVelocity.y);
        FlipSprite(x);
    }

    public void SetVelocityY(float y) => rb.linearVelocity = new Vector2(rb.linearVelocity.x, y);

    private void FlipSprite(float x)
    {
        if (x > 0) transform.localScale = new Vector3(1, 1, 1);
        else if (x < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    public bool IsGrounded()
    {
        if (groundCheck == null) return false;

        // Use OverlapCircle instead of OverlapCircleAll to avoid per-frame allocations.
        Collider2D col = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        return col != null && col.gameObject != gameObject;
    }

    public bool CanParry() => Time.time >= _nextParryTime;
    public void StartParryCooldown() => _nextParryTime = Time.time + parryCooldown;

    public bool CanDash() => Time.time >= _nextDashTime;
    public void StartDashCooldown() => _nextDashTime = Time.time + dashCooldown;

    public void CheckCombo()
    {
        if (Time.time - _lastAttackTime > comboResetTime) _comboCount = 0;
        _comboCount++;
        if (_comboCount > 3) _comboCount = 1;
        _lastAttackTime = Time.time;
    }

    public int GetComboCount() => _comboCount;

    // ------------------------------------------------------------------
    // FIXED: TAKE DAMAGE METHOD (Handles Parry Stun Logic)
    // ------------------------------------------------------------------
    public void TakeDamage(int damage, Collider2D damageSource)
    {
        // Already dead? Ignore further hits.
        if (isDead) return;

        if (isInvulnerable) // Check 1: Dash i-frames
        {
            // Invulnerable (e.g., during dash); ignore the hit.
            return;
        }

        // Check 2: Parry Stun
        if (SM.CurrentState is PlayerParry)
        {
            // Try to get the EnemyController from the object that hit the player
            EnemyController attacker = null;
            if (damageSource != null)
            {
                attacker = damageSource.GetComponentInParent<EnemyController>();
            }

            if (attacker != null)
            {
                // Successful parry: stun the attacker and play parry FX / SFX.
                attacker.OnParryStun();

                if (HitEffectsManager.Instance != null)
                {
                    // Center the effect near the attacker that got parried.
                    HitEffectsManager.Instance.PlayParryEffect(attacker.transform.position);
                }

                return;
            }

            // If attacker is null, we parried environment damage or projectile; no parry FX.
            return;
        }

        // Check 3: Player is Hit (1 HP Rule)
        HandleDeath();
    }
    // ------------------------------------------------------------------

    public void TakeDamage(int damage)
    {
        // This overload handles hits where we don't know the source (e.g., environmental damage)
        TakeDamage(damage, null);
    }

    // Handles the final death sequence, relies on GamePauseManager for UI/Time Freeze
    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        // Stronger camera / screen FX when the player dies.
        if (HitEffectsManager.Instance != null)
        {
            HitEffectsManager.Instance.PlayPlayerDeathEffect(transform.position);
        }

        // Stop all movement and disable sword hitbox
        rb.linearVelocity = Vector2.zero;
        ResetGravity();
        DisableHitbox();

        // Play the death animation, then rely on the manager to freeze time and show UI
        StartCoroutine(DeathSequence());
    }

    // REDIRECTED: This is the old HandleDeath logic, now simplified for animation timing
    private IEnumerator DeathSequence()
    {
        if (anim != null)
        {
            // Play DEATH from the beginning
            anim.speed = 1f;
            anim.Play(AnimDeath, 0, 0f);
        }

        // Let the death animation play once
        yield return new WaitForSeconds(deathAnimationTime);

        // FREEZE WORLD: Call the Singleton manager to show UI and freeze time
        if (GamePauseManager.Instance != null)
        {
            GamePauseManager.Instance.HandleGameOver();
        }
        else
        {
            // Fallback if UIManager is not wired
            Time.timeScale = 0f;
            Debug.LogError("GAME OVER! GamePauseManager not found.");
        }
    }


    public void ResetGravity() => rb.gravityScale = 1f;

    // Toggle physics collision between the player and enemies.
    // This lets us pass through enemies while dashing without falling through the world.
    public void SetEnemyCollision(bool enabled)
    {
        // Require that enemyLayer be set to a single layer in the inspector.
        if (enemyLayer.value == 0) return;

        int enemyLayerIndex = (int)Mathf.Log(enemyLayer.value, 2);
        int playerLayerIndex = gameObject.layer;

        // When enabled == false, ignore collisions so we can phase through enemies.
        Physics2D.IgnoreLayerCollision(playerLayerIndex, enemyLayerIndex, !enabled);
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius); }
    }
}