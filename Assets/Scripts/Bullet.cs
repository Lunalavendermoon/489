// Bullet.cs
using UnityEngine;

public class Bullet : PooledObject
{
    private Vector2 dir;
    private float speed;
    private float damage;
    private GameManager gm;

    [Header("Lifetime")]
    public float maxLifetime = 6f;

    private float life;

    public void Init(Vector2 dir, float speed, float damage, GameManager gm)
    {
        this.dir = dir.sqrMagnitude < 0.001f ? Vector2.right : dir.normalized;
        this.speed = Mathf.Max(0.1f, speed);
        this.damage = Mathf.Max(0f, damage);
        this.gm = gm;
        this.life = 0f;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        life = 0f;
    }

    private void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
        life += Time.deltaTime;

        if (life >= maxLifetime)
            DespawnSelf();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsActive) return;
        if (other == null) return;

        if (other.CompareTag("Cursor"))
        {
            gm.OnPlayerHit();
            DespawnSelf();
        }
    }

    private void DespawnSelf()
    {
        var poolRef = GetComponent<PoolRef>();
        if (poolRef != null) poolRef.Despawn();
        else OnDespawn();
    }
}
