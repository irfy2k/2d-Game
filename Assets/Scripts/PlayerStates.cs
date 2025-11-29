using UnityEngine;

// Player state implementations separated from PlayerController for clarity and scalability.

public class PlayerIdle : State
{
    private PlayerController _pc;
    public PlayerIdle(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.PlayAnim(PlayerController.AnimIdle);
        _pc.SetVelocityX(0);
        _pc.ResetGravity();
        _pc.DisableHitbox(); // Safety net
    }

    public override void LogicUpdate()
    {
        if (_pc.dashInput && _pc.CanDash()) _pc.SM.ChangeState(_pc.DashState);
        else if (_pc.jumpInput && _pc.IsGrounded()) _pc.SM.ChangeState(_pc.JumpState);
        else if (_pc.attackInput) _pc.SM.ChangeState(_pc.AttackState);
        else if (_pc.parryInput && _pc.CanParry()) _pc.SM.ChangeState(_pc.ParryState);
        else if (Mathf.Abs(_pc.moveInput.x) > 0.01f) _pc.SM.ChangeState(_pc.MoveState);
        else if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
    }
}

public class PlayerMove : State
{
    private PlayerController _pc;
    public PlayerMove(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.PlayAnim(PlayerController.AnimRun);
        _pc.ResetGravity();
    }

    public override void LogicUpdate()
    {
        if (_pc.dashInput && _pc.CanDash()) _pc.SM.ChangeState(_pc.DashState);
        else if (_pc.jumpInput && _pc.IsGrounded()) _pc.SM.ChangeState(_pc.JumpState);
        else if (_pc.attackInput) _pc.SM.ChangeState(_pc.AttackState);
        else if (_pc.parryInput && _pc.CanParry()) _pc.SM.ChangeState(_pc.ParryState);
        else if (_pc.moveInput.x == 0) _pc.SM.ChangeState(_pc.IdleState);
        else if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
    }

    public override void PhysicsUpdate() => _pc.SetVelocityX(_pc.moveInput.x * _pc.moveSpeed);
}

public class PlayerJump : State
{
    private PlayerController _pc;
    public PlayerJump(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.SetVelocityY(_pc.jumpForce);
        _pc.jumpInput = false;
        _pc.SM.ChangeState(_pc.AirState);
    }
}

public class PlayerAir : State
{
    private PlayerController _pc;
    public PlayerAir(PlayerController pc) { _pc = pc; }

    public override void Enter() { base.Enter(); }

    public override void LogicUpdate()
    {
        if (_pc.dashInput && _pc.CanDash()) { _pc.SM.ChangeState(_pc.DashState); return; }
        if (_pc.attackInput) { _pc.SM.ChangeState(_pc.AttackState); return; }
        if (_pc.parryInput && _pc.CanParry()) { _pc.SM.ChangeState(_pc.ParryState); return; }

        if (_pc.IsGrounded() && _pc.rb.linearVelocity.y < 0.1f)
        {
            _pc.SM.ChangeState(_pc.IdleState);
            return;
        }

        if (_pc.rb.linearVelocity.y > 0.1f) _pc.PlayAnim(PlayerController.AnimJump);
        else _pc.PlayAnim(PlayerController.AnimFall);
    }

    public override void PhysicsUpdate()
    {
        _pc.SetVelocityX(_pc.moveInput.x * _pc.moveSpeed);

        if (_pc.rb.linearVelocity.y < 0) _pc.rb.gravityScale = _pc.fallMultiplier;
        else if (_pc.rb.linearVelocity.y > 0 && !_pc.isJumpPressed) _pc.rb.gravityScale = _pc.lowJumpMultiplier;
        else _pc.rb.gravityScale = 1f;
    }
}

public class PlayerAttack : State
{
    private PlayerController _pc;
    private float _timer;
    private bool _comboTriggered;

    public PlayerAttack(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.CheckCombo();

        int currentCombo = _pc.GetComboCount();
        if (currentCombo == 1) _pc.PlayAnim(PlayerController.AnimAttack1);
        else if (currentCombo == 2) _pc.PlayAnim(PlayerController.AnimAttack2);
        else _pc.PlayAnim(PlayerController.AnimAttack3);

        _pc.attackInput = false;
        _comboTriggered = false;
        _timer = 0f;
    }

    public override void LogicUpdate()
    {
        _timer += Time.deltaTime;
        if (_pc.attackInput) { _comboTriggered = true; _pc.attackInput = false; }

        if (_pc.dashInput && _pc.CanDash()) { _pc.SM.ChangeState(_pc.DashState); return; }

        if (_timer >= _pc.attackDuration)
        {
            if (_comboTriggered) _pc.SM.ChangeState(_pc.AttackState);
            else
            {
                if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
                else _pc.SM.ChangeState(_pc.IdleState);
            }
        }
        if (_pc.jumpInput && _pc.IsGrounded()) _pc.SM.ChangeState(_pc.JumpState);
    }

    public override void PhysicsUpdate() => _pc.SetVelocityX(_pc.moveInput.x * _pc.moveSpeed);
}

public class PlayerParry : State
{
    private PlayerController _pc;
    private float _timer;

    public PlayerParry(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.PlayAnim(PlayerController.AnimParry);
        _pc.parryInput = false;
        _timer = 0f;
    }

    public override void LogicUpdate()
    {
        _timer += Time.deltaTime;

        if (_pc.dashInput && _pc.CanDash()) { _pc.SM.ChangeState(_pc.DashState); return; }

        if (_timer >= _pc.parryDuration)
        {
            _pc.StartParryCooldown();
            if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
            else _pc.SM.ChangeState(_pc.IdleState);
        }
        if (_pc.jumpInput && _pc.IsGrounded()) _pc.SM.ChangeState(_pc.JumpState);
    }

    public override void PhysicsUpdate()
    {
        _pc.SetVelocityX(_pc.moveInput.x * _pc.moveSpeed);
        if (!_pc.IsGrounded()) _pc.rb.gravityScale = 0.5f;
        else _pc.ResetGravity();
    }
}

public class PlayerDash : State
{
    private PlayerController _pc;
    private float _timer;
    private float _dashDirection;

    public PlayerDash(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.PlayAnim(PlayerController.AnimDash);
        _pc.dashInput = false;
        _pc.StartDashCooldown();
        _timer = 0f;

        if (_pc.moveInput.x != 0) _dashDirection = Mathf.Sign(_pc.moveInput.x);
        else _dashDirection = _pc.transform.localScale.x;

        _pc.rb.gravityScale = 0f;
        _pc.isInvulnerable = true;

        // While dashing, ignore collisions with enemies so we can phase through them.
        _pc.SetEnemyCollision(false);
    }

    public override void Exit()
    {
        base.Exit();

        // Restore normal collisions with enemies when the dash ends.
        _pc.SetEnemyCollision(true);
        _pc.isInvulnerable = false;
    }

    public override void LogicUpdate()
    {
        _timer += Time.deltaTime;

        if (_pc.attackInput)
        {
            _pc.SM.ChangeState(_pc.DashAttackState);
            return;
        }

        if (_timer >= _pc.dashDuration)
        {
            _pc.ResetGravity();
            if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
            else _pc.SM.ChangeState(_pc.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        _pc.SetVelocityX(_dashDirection * _pc.dashSpeed);
        _pc.SetVelocityY(0);
    }
}

public class PlayerDashAttack : State
{
    private PlayerController _pc;
    private float _timer;

    public PlayerDashAttack(PlayerController pc) { _pc = pc; }

    public override void Enter()
    {
        base.Enter();
        _pc.PlayAnim(PlayerController.AnimDashAttack);
        _pc.attackInput = false;
        _timer = 0f;
        _pc.rb.gravityScale = 0f;
        _pc.isInvulnerable = true;
    }

    public override void Exit()
    {
        base.Exit();
        _pc.isInvulnerable = false;
    }

    public override void LogicUpdate()
    {
        _timer += Time.deltaTime;
        if (_timer >= _pc.attackDuration)
        {
            _pc.ResetGravity();
            if (!_pc.IsGrounded()) _pc.SM.ChangeState(_pc.AirState);
            else _pc.SM.ChangeState(_pc.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        float dir = _pc.transform.localScale.x;
        _pc.SetVelocityX(dir * (_pc.dashSpeed * 0.5f));
    }
}
