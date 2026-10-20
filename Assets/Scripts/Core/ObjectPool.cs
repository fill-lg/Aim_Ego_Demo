using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простой универсальный пул объектов для уменьшения аллокаций (GC) в рантайме.
/// </summary>
public class ObjectPool<T> where T : UnityEngine.Object
{
    private readonly T _prefab;
    private readonly Queue<T> _pool = new Queue<T>();
    private readonly Transform _container;

    public ObjectPool(T prefab, int initialSize = 10)
    {
        if (prefab == null)
        {
            Debug.LogError($"ObjectPool: Префаб для пула равен null! Пул не будет инициализирован.");
            return;
        }
        _prefab = prefab;


        // Создаем контейнер в сцене, чтобы не засорять иерархию
        GameObject containerGo = new GameObject($"Pool_{prefab.name}");
        _container = containerGo.transform;

        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    private T CreateNewObject()
    {
        T obj = UnityEngine.Object.Instantiate(_prefab, _container);

        if (obj is GameObject go) go.SetActive(false);
        else if (obj is Component comp) comp.gameObject.SetActive(false);

        _pool.Enqueue(obj);
        return obj;
    }

    public T Get()
    {
        T obj = _pool.Count > 0 ? _pool.Dequeue() : UnityEngine.Object.Instantiate(_prefab, _container);

        if (obj is GameObject go) go.SetActive(true);
        else if (obj is Component comp) comp.gameObject.SetActive(true);

        return obj;
    }

    public void Return(T obj)
    {
        if (obj is GameObject go) go.SetActive(false);
        else if (obj is Component comp) comp.gameObject.SetActive(false);

        _pool.Enqueue(obj);
    }
}
