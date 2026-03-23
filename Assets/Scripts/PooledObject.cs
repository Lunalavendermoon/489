// PooledObject.cs
using UnityEngine;

public abstract class PooledObject : MonoBehaviour
{
    public bool IsActive { get; private set; }

    public virtual void OnSpawn()
    {
        IsActive = true;
        gameObject.SetActive(true);
    }

    public virtual void OnDespawn()
    {
        IsActive = false;
        gameObject.SetActive(false);
    }
}
