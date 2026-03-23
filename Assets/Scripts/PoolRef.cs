// PoolRef.cs
using UnityEngine;

public class PoolRef : MonoBehaviour
{
    public System.Action despawnAction;

    public void Despawn()
    {
        despawnAction?.Invoke();
    }
}
