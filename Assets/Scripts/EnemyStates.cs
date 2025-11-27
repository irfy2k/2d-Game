using UnityEngine;
using System.Collections;

// Enemy state implementations separated from EnemyController for clarity and scalability.

public abstract class EnemyState : State
{
    protected EnemyController _ec;
    public EnemyState(EnemyController ec) { _ec = ec; }
}

public class EnemyIdle : EnemyState
{
    public EnemyIdle(EnemyController ec) : base(ec) { }

    public override void Enter()
    {
        // FIX: Use Kinematic for Idle to prevent lag and physics drag
        _ec.rb.bodyType = RigidbodyType2D.Kinematic;
        _ec.PlayAnim(EnemyController.AnimIdle);
        _ec.StopMovement();
    }

    public override void LogicUpdate()
    {
        // If the player is dead, stay idle.
        if (_ec.playerController != null && _ec.playerController.isDead) return;

        if (_ec.GetSqrDistanceToPlayer() < _ec.DetectionRangeSqr)
        {
            _ec.SM.ChangeState(_ec.ChaseState);
        }
    }
}

public class EnemyChase : EnemyState
{
    public EnemyChase(EnemyController ec) : base(ec) { }

    public override void Enter()
    {
        // Restore Dynamic physics when moving
        _ec.rb.bodyType = RigidbodyType2D.Dynamic;
        _ec.PlayAnim(EnemyController.AnimRun);
    }

    public override void LogicUpdate()
    {
        // If the player is dead, stop chasing and idle.
        if (_ec.playerController != null && _ec.playerController.isDead)
        {
            _ec.StopMovement();
            _ec.SM.ChangeState(_ec.IdleState);
            return;
        }

        float distSqr = _ec.GetSqrDistanceToPlayer();

        if (distSqr > _ec.DetectionRangeSqr)
        {
            _ec.SM.ChangeState(_ec.IdleState);
            return;
        }

        // Special case: if the player is dashing TOWARD us and is reasonably close,
        // stop chasing and perform an in-place attack to catch them off guard.
        if (_ec.playerController != null && _ec.player != null)
        {
            State playerState = _ec.playerController.SM.CurrentState;
            bool isDashing = playerState is PlayerDash || playerState is PlayerDashAttack;

            if (isDashing)
            {
                float dirToPlayer = Mathf.Sign(_ec.player.position.x - _ec.transform.position.x);
                float playerVelX = _ec.playerController.rb.linearVelocity.x;

                if (Mathf.Abs(playerVelX) > 0.1f && Mathf.Sign(playerVelX) == dirToPlayer)
                {
                    // Player is moving toward us; counter-attack if within extended range.
                    float maxCounterRange = _ec.attackRange * 1.6f;
                    if (distSqr <= maxCounterRange * maxCounterRange)
                    {
                        _ec.StopMovement();
                        _ec.SM.ChangeState(_ec.AttackState);
                        return;
                    }
                }
            }
        }

        if (distSqr <= _ec.AttackRangeSqr)
        {
            _ec.SM.ChangeState(_ec.AttackState);
        }
    }

    public override void PhysicsUpdate()
    {
        if (_ec.playerController != null && _ec.playerController.isDead)
        {
            _ec.StopMovement();
            return;
        }

        _ec.MoveTowardsPlayer(_ec.player.position.x);
    }
}

public class EnemyAttack : EnemyState
{
    public EnemyAttack(EnemyController ec) : base(ec) { }

    public override void Enter()
    {
        // If the player is already dead, don't bother attacking.
        if (_ec.playerController != null && _ec.playerController.isDead)
        {
            _ec.SM.ChangeState(_ec.IdleState);
            return;
        }

        _ec.StartTrackedCoroutine(AttackSequence());
    }

    public override void Exit()
    {
        _ec.DisableHitbox();
        _ec.StopMovement();
    }

    private IEnumerator AttackSequence()
    {
        // 1. Windup (Telegraph)
        _ec.StopMovement();
        _ec.PlayAnim(EnemyController.AnimIdle);
        _ec.sr.color = Color.yellow;
        yield return new WaitForSeconds(_ec.attackWindup);
        _ec.sr.color = Color.white;

        // 2. Attack (Animation Events now control EnableHitbox)
        _ec.PlayAnim(EnemyController.AnimAttack);
        yield return new WaitForSeconds(0.2f);

        // 3. Cooldown
        yield return new WaitForSeconds(_ec.attackCooldown);

        // If the player died during this attack, stop attacking and idle.
        if (_ec.playerController != null && _ec.playerController.isDead)
        {
            _ec.DisableHitbox();
            _ec.StopMovement();
            _ec.SM.ChangeState(_ec.IdleState);
            yield break;
        }

        float maxRange = _ec.attackRange + 0.5f;
        float distSqr = _ec.GetSqrDistanceToPlayer();
        if (distSqr <= maxRange * maxRange)
        {
            _ec.SM.ChangeState(_ec.AttackState);
        }
        else
        {
            _ec.SM.ChangeState(_ec.ChaseState);
        }
    }
}

public class EnemyStun : EnemyState
{
    public EnemyStun(EnemyController ec) : base(ec) { }

    public override void Enter()
    {
        _ec.PlayAnim(EnemyController.AnimHurt);
        _ec.StopMovement();
        // Lock body completely on stun
        _ec.rb.bodyType = RigidbodyType2D.Kinematic;
        _ec.sr.color = Color.blue;

        _ec.StartTrackedCoroutine(StunSequence());
    }

    public override void Exit()
    {
        _ec.sr.color = Color.white;
        _ec.rb.bodyType = RigidbodyType2D.Dynamic; // Ready to move again
    }

    private IEnumerator StunSequence()
    {
        yield return new WaitForSeconds(_ec.stunDuration);
        _ec.SM.ChangeState(_ec.ChaseState);
    }
}

public class EnemyDead : EnemyState
{
    public EnemyDead(EnemyController ec) : base(ec) { }

    public override void Enter()
    {
        // Ensure the body is dynamic so it can be knocked back and fall under gravity
        _ec.rb.bodyType = RigidbodyType2D.Dynamic;

        // Disable the attack hitbox so it can't damage the player anymore
        _ec.DisableHitbox();

        // Apply knockback and let physics handle the arc and fall
        _ec.ApplyKnockback();

        // Start playing the death animation immediately while it is flying/falling
        _ec.PlayAnim(EnemyController.AnimDeath);

        // Start the death sequence timer
        _ec.StartTrackedCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Let physics run while the death animation plays
        yield return new WaitForSeconds(_ec.deathAnimationTime);

        // Optional: stop any residual movement just before destroying
        _ec.rb.linearVelocity = Vector2.zero;

        GameObject.Destroy(_ec.gameObject);
    }
}
