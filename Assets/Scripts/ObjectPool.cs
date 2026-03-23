// ObjectPool.cs
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : PooledObject
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Queue<T> inactive = new Queue<T>();
    private readonly List<T> all = new List<T>();

    public ObjectPool(T prefab, int prewarm, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarm; i++)
        {
            var obj = Object.Instantiate(prefab, parent);
            obj.OnDespawn();
            inactive.Enqueue(obj);
            all.Add(obj);
        }
    }

    public T Spawn(Vector3 position, Quaternion rotation)
    {
        T obj = inactive.Count > 0 ? inactive.Dequeue() : Object.Instantiate(prefab, parent);
        if (!all.Contains(obj)) all.Add(obj);

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.OnSpawn();
        return obj;
    }

    public void Despawn(T obj)
    {
        if (obj == null) return;
        obj.OnDespawn();
        inactive.Enqueue(obj);
    }

    public void DespawnAllActive()
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].gameObject.activeSelf)
                Despawn(all[i]);
        }
    }
}
