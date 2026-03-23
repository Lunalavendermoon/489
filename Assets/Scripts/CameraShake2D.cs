// CameraShake2D.cs
using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
    private Vector3 basePos;
    private float timer;
    private float strength;

    private void Awake()
    {
        basePos = transform.localPosition;
    }

    public void Shake(float strength, float duration)
    {
        this.strength = Mathf.Max(this.strength, strength);
        timer = Mathf.Max(timer, duration);
    }

    private void LateUpdate()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            Vector2 r = Random.insideUnitCircle * strength;
            transform.localPosition = basePos + new Vector3(r.x, r.y, 0f);

            if (timer <= 0f)
            {
                transform.localPosition = basePos;
                strength = 0f;
            }
        }
    }
}
