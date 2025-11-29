using UnityEngine;

public class DrawHitbox : MonoBehaviour
{
    // Color of the box (Red with some transparency)
    public Color gizmoColor = new Color(1, 0, 0, 0.5f);

    private void OnDrawGizmos()
    {
        // Get the collider attached to this object
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (col != null)
        {
            // Set the color
            Gizmos.color = gizmoColor;

            // Draw a cube at the collider's position with its size
            // We need to account for the offset if you set one
            Vector3 center = transform.position + (Vector3)col.offset;
            Gizmos.DrawCube(center, col.size);

            // Draw a wireframe outline too so it's easy to see boundaries
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, col.size);
        }
    }
}