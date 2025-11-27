using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    public int damageAmount = 1; // Set to 1 for the 1 HP rule
    public LayerMask targetLayer;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. Check if the object we hit is on the specified Target Layer.
        if (((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            // 2. Try to get the IDamageable component (Player/Enemies implement this).
            // We use GetComponentInParent to handle hitting child hitboxes (like the Goblin's).
            IDamageable target = collision.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                // 3. CRITICAL: Check if the target is the PlayerController.
                PlayerController pc = target as PlayerController;

                if (pc != null)
                {
                    // If the target is the Player, call the special version that passes the SOURCE COLLIDER.
                    // Use this object's collider as the damage source so we can find the attacker for parry stun.
                    Collider2D sourceCollider = GetComponent<Collider2D>();
                    if (sourceCollider == null)
                    {
                        // Fallback for misconfigured objects; parry stun may not work in this case.
                        sourceCollider = collision;
                    }

                    pc.TakeDamage(damageAmount, sourceCollider);
                }
                else
                {
                    // If the target is not the Player (it must be the Goblin/Enemy), call the simple version.
                    target.TakeDamage(damageAmount);
                }
            }
        }
    }
}