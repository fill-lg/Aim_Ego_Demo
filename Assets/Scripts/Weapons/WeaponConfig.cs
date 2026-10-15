using UnityEngine;

/// <summary>
/// Конфиг оружия. Вся балансировка — в ассетах (Assets/Settings/Weapons),
/// код один и тот же для пистолета и автомата.
/// </summary>
[CreateAssetMenu(fileName = "WEAP_New", menuName = "AimEgo/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Идентификация")]
    public string displayName = "Оружие";
    [Tooltip("Автомат: огонь при удержании ЛКМ. Выкл — один выстрел на нажатие")]
    public bool automatic = false;

    [Header("Урон и дальность")]
    public float damage = 30f;
    [Tooltip("Дальность хитскан-рейкаста, м")]
    public float range = 300f;

    [Header("Огонь")]
    [Tooltip("Секунд между выстрелами (0.1 = 600 выстрелов/мин)")]
    public float fireInterval = 0.15f;
    public int magazineSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2.4f;

    [Header("Разброс (в градусах)")]
    [Tooltip("Базовый разброс первого выстрела")]
    public float baseSpread = 0.2f;
    [Tooltip("Добавка разброса за каждый выстрел")]
    public float spreadPerShot = 0.4f;
    public float maxSpread = 4f;
    [Tooltip("Восстановление разброса, градусов в секунду")]
    public float spreadRecovery = 6f;

    [Header("Отдача (в градусах)")]
    [Tooltip("Подброс камеры вверх за выстрел")]
    public float recoilPitch = 1.5f;
    [Tooltip("Случайный увод камеры в сторону за выстрел")]
    public float recoilYaw = 0.3f;

    [Header("Вьюмодель")]
    public GameObject viewModelPrefab;
    [Tooltip("Позиция дула относительно корня вьюмодели (там же вспышка и старт трассера)")]
    public Vector3 muzzleLocalPosition = new Vector3(0f, 0.05f, 0.45f);

    [Header("Эффекты")]
    public Color tracerColor = new Color(1f, 0.85f, 0.55f, 0.9f);
    public float tracerWidth = 0.02f;
    public float tracerTime = 0.05f;

    [Header("Звук (заполняет сетап-скрипт фазы 1.6)")]
    public AudioClip fireSound;
    [Tooltip("Щелчок при выстреле с пустым магазином")]
    public AudioClip dryFireSound;
    [Tooltip("Магазин вышел (начало перезарядки)")]
    public AudioClip magOutSound;
    [Tooltip("Магазин вставлен")]
    public AudioClip magInSound;
    [Tooltip("Передёргивание затвора (конец перезарядки)")]
    public AudioClip rackSound;
}
