using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Отвечает за все визуальные и звуковые эффекты оружия.
/// Отделяет логику стрельбы от визуализации.
/// </summary>
public class WeaponEffects : MonoBehaviour
{
    [Header("Префабы и материалы")]
    public GameObject impactPrefab;
    public Material tracerMaterial;
    public Material decalMaterial;
    public float decalSize = 0.15f;
    public float decalLifetime = 12f;

    private ObjectPool<BulletDecal> _decalPool;
    private ObjectPool<GameObject> _impactPool; // В Unity GameObject не Component, используем обертку или упростим

    void Awake()
    {
        // Создаем простой Quad меш вручную, чтобы избежать проблем с Resources.GetBuiltinResource
        Mesh quadMesh = new Mesh();
        Vector3[] vertices = {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };
        Vector2[] uv = {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        int[] triangles = { 0, 2, 1, 1, 2, 3 };

        quadMesh.vertices = vertices;
        quadMesh.uv = uv;
        quadMesh.triangles = triangles;
        quadMesh.RecalculateNormals();

        GameObject decalPrefab = new GameObject("DecalPrefab");
        decalPrefab.AddComponent<BulletDecal>();
        var meshFilter = decalPrefab.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quadMesh;
        var meshRenderer = decalPrefab.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = decalMaterial;

        _decalPool = new ObjectPool<BulletDecal>(decalPrefab.GetComponent<BulletDecal>(), 20);

        // Для частиц используем обычный Instantiate, так как они часто имеют свои системы пулинга
        // или мы можем реализовать отдельный пул GameObject.
    }


    public void PlayMuzzleFlash(MuzzleFlash flash)
    {
        if (flash != null) flash.Show();
    }

    public void PlayTracer(Vector3 start, Vector3 end, WeaponConfig config)
    {
        TracerEffect.Spawn(start, end, tracerMaterial, config.tracerWidth, config.tracerTime, config.tracerColor);
    }

    public void PlayImpact(RaycastHit hit, WeaponConfig config)
    {
        // Декаль из пула
        var decal = _decalPool.Get();
        decal.transform.position = hit.point + hit.normal * 0.01f;
        decal.transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

        // Масштаб
        float s = decalSize * UnityEngine.Random.Range(0.8f, 1.2f);
        decal.transform.localScale = new Vector3(s, s, s);

        decal.Initialize(_decalPool, decalLifetime);

        // Частицы
        if (impactPrefab != null)
        {
            var impact = Instantiate(impactPrefab, hit.point + hit.normal * 0.02f, Quaternion.LookRotation(hit.normal));
            Destroy(impact, 1.5f);
        }
    }
}
