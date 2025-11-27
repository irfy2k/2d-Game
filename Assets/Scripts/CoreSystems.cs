using UnityEngine;

// 1. INTERFACE
// This allows the player, enemies, and breakable objects to all share "Health" logic.
public interface IDamageable
{
    void TakeDamage(int damage);
}

// 2. STATE BASE CLASS
// This is the template for every state (Idle, Run, Jump, etc.)
public abstract class State
{
    protected float timeEntered;

    // Called once when the state starts
    public virtual void Enter()
    {
        timeEntered = Time.time;
    }

    // Called once when the state ends
    public virtual void Exit() { }

    // Called every frame (runs in Update)
    public virtual void LogicUpdate() { }

    // Called every physics frame (runs in FixedUpdate)
    public virtual void PhysicsUpdate() { }
}

// 3. STATE MACHINE HANDLER
// This class manages switching between states.
public class StateMachine
{
    public State CurrentState { get; private set; }

    public void Initialize(State startingState)
    {
        CurrentState = startingState;
        CurrentState.Enter();
    }

    public void ChangeState(State newState)
    {
        // Exit the old state
        CurrentState.Exit();

        // Swap to the new state
        CurrentState = newState;

        // Enter the new state
        CurrentState.Enter();
    }
}