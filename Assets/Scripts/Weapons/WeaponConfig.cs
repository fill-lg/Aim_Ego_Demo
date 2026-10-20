using UnityEngine;

/// <summary>
/// Конфиг оружия. Вся балансировка — в ассетах (Assets/Settings/Weapons),
/// код один и тот же для пистолета и автомата.
/// </summary>
public enum WeaponType { Firearm, Melee }

[CreateAssetMenu(fileName = "WEAP_New", menuName = "AimEgo/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Идентификация")]
    public WeaponType weaponType = WeaponType.Firearm;
    public string displayName = "Оружие";
    [Tooltip("Можно ли выбросить это оружие (для ножа должно быть false)")]
    public bool isDroppable = true;
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

    [Tooltip("Рисунок разброса (спрей). Каждый элемент — смещение (X: Yaw, Y: Pitch).")]
    public Vector2[] sprayPattern;


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

    [Header("Прицеливание (ADS)")]
    [Tooltip("Множитель разброса при прицеливании (например, 0.5 = в 2 раза точнее)")]
    public float adsSpreadMultiplier = 0.5f;
    [Tooltip("Множитель отдачи при прицеливании")]
    public float adsRecoilMultiplier = 0.7f;
    [Tooltip("Смещение вьюмодели в прицеле (relative to basePos)")]
    public Vector3 adsPosition = new Vector3(0f, -0.1f, 0.1f);
    [Tooltip("Поворот вьюмодели в прицеле")]
    public Vector3 adsRotation = new Vector3(0f, 0f, 0f);
    [Tooltip("Поле зрения камеры в прицеле")]
    public float adsFOV = 40f;
    [Tooltip("Скорость перехода в прицел")]
    public float adsTransitionSpeed = 10f;
}
