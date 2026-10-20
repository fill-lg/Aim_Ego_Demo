using UnityEngine;

/// <summary>
/// Предмет, лежащий в мире. Содержит информацию о том, какое оружие он представляет.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ItemPickup : MonoBehaviour
{
    [Header("Настройки")]
    public WeaponConfig config;
    [Tooltip("Префаб визуальной модели оружия на земле")]
    public GameObject worldModelPrefab;

    private GameObject _spawnedModel;
    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Настраиваем физику для реалистичного падения
        _rb.mass = 1.0f;
        _rb.linearDamping = 0.5f;
        _rb.angularDamping = 0.05f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Start()
    {
        if (config == null) return;

        // Если есть префаб модели для мира, спавним его
        if (worldModelPrefab != null)
        {
            _spawnedModel = Instantiate(worldModelPrefab, transform);
            _spawnedModel.transform.localPosition = Vector3.zero;
            _spawnedModel.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // Если нет, используем вьюмодель как временную заплатку
            if (config.viewModelPrefab != null)
            {
                _spawnedModel = Instantiate(config.viewModelPrefab, transform);
                _spawnedModel.transform.localPosition = Vector3.zero;
                _spawnedModel.transform.localRotation = Quaternion.identity;

                // Отключаем скрипты вьюмодели, чтобы они не работали в мире
                if (_spawnedModel.TryGetComponent<WeaponViewModel>(out var vm))
                    Destroy(vm);
            }
        }

        UpdateColliderToFitModel();
    }

    private void UpdateColliderToFitModel()
    {
        if (_spawnedModel == null) return;

        // Находим все Renderer-ы в модели, чтобы вычислить общий размер (Bounds)
        Renderer[] renderers = _spawnedModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        // Получаем размер модели в локальных координатах
        Vector3 size = combinedBounds.size;

        // Настраиваем BoxCollider
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();

        col.size = size;

        // Смещаем центр коллайдера, чтобы он совпадал с центром модели
        // bounds.center находится в мировых координатах, переводим в локальные
        Vector3 localCenter = transform.InverseTransformPoint(combinedBounds.center);
        col.center = localCenter;
    }

    public void Despawn()
    {
        Destroy(gameObject);
    }
}
