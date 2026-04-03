using UnityEngine;

public class CamFollow : MonoBehaviour
{
    private float offsetX = 2.5f;
    public Player target;

    void Update()
    {
        Vector3 stable = target.StablePosition;
        transform.position = new Vector3(stable.x + offsetX, stable.y, transform.position.z);
    }
}