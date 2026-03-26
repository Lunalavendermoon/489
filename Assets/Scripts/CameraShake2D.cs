// CameraShake2D.cs
using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
    private Vector3 basePos;
    private float timer;
    private float strength;
    
    private const float MAX_SHAKE_STRENGTH = 0.8f;
    private const float MAX_SHAKE_DURATION = 0.25f;

    private void Awake()
    {
        basePos = transform.localPosition;
    }

    public void Shake(float strength, float duration)
    {
        this.strength = Mathf.Min(Mathf.Max(this.strength, strength), MAX_SHAKE_STRENGTH);
        timer = Mathf.Min(Mathf.Max(timer, duration), MAX_SHAKE_DURATION);
    }

    public void ResetShake()
    {
        timer = 0f;
        strength = 0f;
        transform.localPosition = basePos;
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
