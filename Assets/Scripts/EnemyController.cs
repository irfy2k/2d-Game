using UnityEngine;
using System.Collections;
using System;

// This implements the IDamageable rule from CoreSystems.cs
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float speed = 3.5f;
    public float detectionRange = 7f;
    public float attackRange = 1.0f;
    public int health = 1; // 1 HP Rule!
    public float knockbackForce = 18f; // Force to apply on death

    [Header("Timers")]
    public float stunDuration = 1.4f;
    public float attackWindup = 0.35f; // Faster telegraph for a snappier feel
    public float attackCooldown = 0.7f;
    public float deathAnimationTime = 0.8f; // Slightly faster death animation

    [Header("References")]
    public Rigidbody2D rb;
    public Animator anim;
    public Transform player; // Drag the Knight here
    public Collider2D attackHitbox;
    public SpriteRenderer sr;

    // Animation state hashes to avoid repeated string lookups.
    public static readonly int AnimIdle = Animator.StringToHash("IDLE");
    public static readonly int AnimRun = Animator.StringToHash("RUN");
    public static readonly int AnimAttack = Animator.StringToHash("ATTACK");
    public static readonly int AnimHurt = Animator.StringToHash("HURT");
    public static readonly int AnimDeath = Animator.StringToHash("DEATH");

    // Cached reference to the player's controller (for reading dash state)
    public PlayerController playerController;

    // --- State Machine ---
    public StateMachine SM;
    public EnemyIdle IdleState;
    public EnemyChase ChaseState;
    public EnemyAttack AttackState;
    public EnemyStun StunState;
    public EnemyDead DeadState;

    // Tracks the current attack coroutine
    private IEnumerator _currentRoutine;

    private void Awake()
    {
        SM = new StateMachine();
        IdleState = new EnemyIdle(this);
        ChaseState = new EnemyChase(this);
        AttackState = new EnemyAttack(this);
        StunState = new EnemyStun(this);
        DeadState = new EnemyDead(this);

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (attackHitbox != null) attackHitbox.enabled = false;
    }

    private void Start()
    {
        // Safety check to find the player by Tag if not manually assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Cache the player's controller and disable body-vs-body collision so the goblin
        // can't physically push the player around (attacks still use separate hitboxes).
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();

            // Ignore collisions between ALL solid (non-trigger) colliders on the enemy
            // and ALL solid colliders on the player. This stays valid even if you
            // swap BoxCollider2D <-> CapsuleCollider2D or move colliders to children.
            Collider2D[] enemyCols = GetComponentsInChildren<Collider2D>();
            Collider2D[] playerCols = player.GetComponentsInChildren<Collider2D>();

            foreach (Collider2D eCol in enemyCols)
            {
                if (eCol == null || eCol.isTrigger) continue;

                foreach (Collider2D pCol in playerCols)
                {
                    if (pCol == null || pCol.isTrigger) continue;

                    Physics2D.IgnoreCollision(eCol, pCol, true);
                }
            }
        }

        SM.Initialize(IdleState);
    }

    private void Update()
    {
        SM.CurrentState.LogicUpdate();
    }

    private void FixedUpdate()
    {
        SM.CurrentState.PhysicsUpdate();
    }

    // --- ANIMATION EVENT METHODS ---
    public void EnableHitbox()
    {
        if (attackHitbox != null) attackHitbox.enabled = true;
    }

    public void DisableHitbox()
    {
        if (attackHitbox != null) attackHitbox.enabled = false;
    }

    public void PlayAnim(int stateHash)
    {
        anim.Play(stateHash);
    }
    // --------------------------------------------------------

    // --- IDamageable Interface Implementation ---
    public void TakeDamage(int damage)
    {
        if (SM.CurrentState is EnemyDead) return;

        health -= damage;

        if (health <= 0)
        {
            StopAllCoroutines(); // Stop existing routines before death
            SM.ChangeState(DeadState);
        }
    }

    // Called by the PlayerController when a Parry is successful
    public void OnParryStun()
    {
        if (SM.CurrentState != StunState && SM.CurrentState != DeadState)
        {
            StopAllCoroutines(); // Stop any attack in progress
            SM.ChangeState(StunState);
        }
    }

    // Apply knockback motion away from the direction the enemy is facing.
    public void ApplyKnockback()
    {
        // Direction is opposite of current facing direction (localScale.x)
        float direction = -Mathf.Sign(transform.localScale.x == 0 ? 1 : transform.localScale.x);

        rb.bodyType = RigidbodyType2D.Dynamic; // Ensure it can receive force/movement

        // Directly set a strong velocity so the knockback is always visible,
        // regardless of mass/drag settings.
        rb.linearVelocity = new Vector2(direction * knockbackForce, knockbackForce * 0.5f);
    }


    // Helper functions
    public void MoveTowardsPlayer(float targetX)
    {
        if (player == null) return;

        float dir = Mathf.Sign(targetX - transform.position.x);
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        transform.localScale = new Vector3(dir, 1, 1);
    }

    public void StopMovement()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    public float GetDistanceToPlayer()
    {
        return Mathf.Sqrt(GetSqrDistanceToPlayer());
    }

    public float GetSqrDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        if (playerController != null && playerController.isDead) return float.MaxValue;
        Vector2 delta = player.position - transform.position;
        return delta.sqrMagnitude;
    }

    public float DetectionRangeSqr => detectionRange * detectionRange;
    public float AttackRangeSqr => attackRange * attackRange;

    // Coroutine Tracker
    public void StartTrackedCoroutine(IEnumerator routine)
    {
        StopAllCoroutines();
        _currentRoutine = routine;
        StartCoroutine(_currentRoutine);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}


