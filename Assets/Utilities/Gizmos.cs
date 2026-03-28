using UnityEngine;

public static class GizmoUtils
{
    public static void DrawWheelGizmo(Transform wheel, Transform chassis, LayerMask groundLayer)
    {
        if (wheel == null) return;
        var collider = wheel.GetComponent<CircleCollider2D>();
        float worldRadius = collider != null ? collider.radius * wheel.lossyScale.x : 0.1f;
        Vector2 origin = wheel.position;
        float castDist = worldRadius + 0.05f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, castDist, groundLayer);
        if (hit.collider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, hit.point);
            Gizmos.DrawWireSphere(hit.point, 0.05f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + Vector2.down * castDist);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(wheel.position, worldRadius);
    }
}
