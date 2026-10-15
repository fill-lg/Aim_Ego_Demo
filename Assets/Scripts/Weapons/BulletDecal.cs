using UnityEngine;
using System.Collections;

/// <summary>
/// Компонент для декалей попаданий.
/// Сам возвращает себя в пул по истечении времени жизни.
/// </summary>
public class BulletDecal : MonoBehaviour
{
    private ObjectPool<BulletDecal> _originPool;
    private float _lifetime;

    public void Initialize(ObjectPool<BulletDecal> pool, float lifetime)
    {
        _originPool = pool;
        _lifetime = lifetime;
        StartCoroutine(ReturnAfterTime());
    }

    private IEnumerator ReturnAfterTime()
    {
        yield return new WaitForSeconds(_lifetime);
        _originPool?.Return(this);
    }
}
