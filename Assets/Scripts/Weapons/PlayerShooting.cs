using System;
using UnityEngine;

/// <summary>
/// Стрельба игрока: слоты оружия, hitscan-выстрелы с разбросом,
/// отдача, перезарядка, эффекты. Ввод берётся из PlayerInputHandler.
/// В фазе 2 Fire() превратится в сетевой вызов — логика уже выделена.
/// </summary>
public class PlayerShooting : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerInputHandler input;
    [Tooltip("Камера игрока — из неё летит рейкаст")]
    public Camera eyes;
    [Tooltip("WeaponHolder под камерой — туда спавнятся вьюмодели")]
    public Transform weaponHolder;

    [Header("Снаряжение (слоты 1 и 2)")]
    public WeaponConfig[] loadout = new WeaponConfig[2];

    [Header("Эффекты попаданий")]
    public Material tracerMaterial;
    public Material decalMaterial;
    [Tooltip("Префаб всплеска частиц при попадании в геометрию")]
    public GameObject impactPrefab;
    public float decalSize = 0.15f;
    public float decalLifetime = 12f;

    [Header("Смена оружия")]
    public float equipTime = 0.5f;

    [Header("Звук")]
    [Tooltip("2D-источник для щелчков и звуков перезарядки. Пусто — создастся сам")]
    public AudioSource sfx;

    // ===== Состояние =====
    private GameObject[] viewModels;
    private int currentSlot;
    private int[] mag;
    private int[] reserve;
    private float bloom;          // накопленный разброс
    private float cooldown;       // до следующего выстрела
    private float equipTimer;     // время после смены оружия
    private float reloadTimer;
    private bool reloading;
    private int reloadStage;    // какой звук перезарядки уже сыгран
    private int shotsFiredInBurst; // счетчик выстрелов в очереди для спрея
    private float lastShotTime;
    private Transform muzzle;
    private MuzzleFlash flash;
    private WeaponEffects effects;


    // Доли ReloadProgress, на которых играют звуки этапов (синхронно с анимацией вьюмодели)
    static readonly float[] ReloadStages = { WeaponViewModel.TMagOut, WeaponViewModel.TMagIn, WeaponViewModel.TRack };

    private static readonly RaycastHit[] hits = new RaycastHit[32];
    private static readonly DistanceComparer comparer = new DistanceComparer();

    // ===== Публичное (для HUD и сети) =====
    public WeaponConfig Config => loadout != null && currentSlot < loadout.Length ? loadout[currentSlot] : null;
    public int CurrentSlot => currentSlot;
    public int CurrentMag => mag != null ? mag[currentSlot] : 0;
    public int CurrentReserve => reserve != null ? reserve[currentSlot] : 0;
    public float Bloom => bloom;
    public bool IsReloading => reloading;
    public float ReloadProgress => Config != null && Config.reloadTime > 0f ? 1f - reloadTimer / Config.reloadTime : 0f;

    public event Action AmmoChanged;
    public event Action WeaponSwitched;
    /// (нанесённый урон, убил ли этим выстрелом) — для хитмаркера
    public event Action<float, bool> HitConfirmed;
    public event Action<bool> ReloadStateChanged;
    public event Action BloomChanged;


    void Start()
    {
        if (input == null) input = GetComponent<PlayerInputHandler>();
        if (eyes == null) eyes = GetComponentInChildren<Camera>();
        if (weaponHolder == null && eyes != null) weaponHolder = eyes.transform.Find("WeaponHolder");
        if (sfx == null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;   // щелчки — 2D, прямо в уши
            sfx.playOnAwake = false;
            sfx.dopplerLevel = 0f;
        }

        effects = GetComponent<WeaponEffects>();
        if (effects == null) effects = gameObject.AddComponent<WeaponEffects>();

        int n = loadout != null ? loadout.Length : 0;
        viewModels = new GameObject[n];
        mag = new int[n];
        reserve = new int[n];


        for (int i = 0; i < n; i++)
        {
            var cfg = loadout[i];
            if (cfg == null) continue;

            mag[i] = cfg.magazineSize;
            reserve[i] = cfg.reserveAmmo;

            if (cfg.viewModelPrefab != null && weaponHolder != null)
            {
                var go = Instantiate(cfg.viewModelPrefab, weaponHolder);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                SetLayerRecursive(go, LayerMask.NameToLayer("ViewModel"));

                var vmAnim = go.GetComponent<WeaponViewModel>();
                if (vmAnim != null)
                {
                    vmAnim.input = input;
                    vmAnim.shooting = this;
                    vmAnim.controller = GetComponent<PlayerController>();
                }
                viewModels[i] = go;
                go.SetActive(i == currentSlot);
            }
        }

        equipTimer = equipTime;
        CacheMuzzle();
        AmmoChanged?.Invoke();
    }

    void Update()
    {
        if (input == null || Config == null) return;

        float dt = Time.deltaTime;
        cooldown -= dt;
        equipTimer -= dt;

        // Разброс восстанавливается, когда не стреляешь
        float prevBloom = bloom;
        bloom = Mathf.MoveTowards(bloom, 0f, Config.spreadRecovery * dt);
        if (Mathf.Abs(bloom - prevBloom) > 0.001f) BloomChanged?.Invoke();

        HandleSwitch();

        if (input.ReloadPressed) TryReload();

        if (reloading)
        {
            reloadTimer -= dt;

            // Звуки этапов точно по таймлайну анимации вьюмодели
            float p = ReloadProgress;
            while (reloadStage < ReloadStages.Length && p >= ReloadStages[reloadStage])
            {
                PlaySfx(reloadStage == 0 ? Config.magOutSound
                     : reloadStage == 1 ? Config.magInSound
                     : Config.rackSound);
                reloadStage++;
            }

            if (reloadTimer <= 0f) FinishReload();
        }
        else if (reloadStage != 0)
        {
            reloadStage = 0; // перезарядка отменена свапом — сброс
        }

        HandleFire();
    }


    // ===== СМЕНА ОРУЖИЯ =====
    void HandleSwitch()
    {
        int target = currentSlot;
        if (input.Slot1Pressed && loadout.Length > 0) target = 0;
        else if (input.Slot2Pressed && loadout.Length > 1) target = 1;
        else if (input.QuickSwapPressed) target = currentSlot == 0 ? 1 : 0; // Q — быстрый свап

        if (target == currentSlot || loadout[target] == null) return;

        Debug.Log("Оружие: " + loadout[target].displayName, this);

        if (viewModels[currentSlot] != null) viewModels[currentSlot].SetActive(false);
        currentSlot = target;
        if (viewModels[currentSlot] != null) viewModels[currentSlot].SetActive(true);

        reloading = false; // смена оружия отменяет перезарядку
        bloom = 0f;
        cooldown = 0f;
        equipTimer = equipTime;
        CacheMuzzle();
        WeaponSwitched?.Invoke();
        AmmoChanged?.Invoke();
    }

    void CacheMuzzle()
    {
        muzzle = null;
        flash = null;
        var go = viewModels != null && currentSlot < viewModels.Length ? viewModels[currentSlot] : null;
        if (go != null)
        {
            muzzle = go.transform.Find("Muzzle");
            flash = go.GetComponentInChildren<MuzzleFlash>(true);
        }
        if (muzzle == null && eyes != null) muzzle = eyes.transform;
    }

    // ===== ПЕРЕЗАРЯДКА =====
    void TryReload()
    {
        if (reloading || Config == null) return;
        if (mag[currentSlot] >= Config.magazineSize || reserve[currentSlot] <= 0) return;

        reloading = true;
        ReloadStateChanged?.Invoke(true);
        reloadStage = 0;
        reloadTimer = Config.reloadTime;
    }

    void FinishReload()
    {
        reloading = false;
        ReloadStateChanged?.Invoke(false);
        if (Config == null) return;

        int need = Config.magazineSize - mag[currentSlot];
        int take = Mathf.Min(need, reserve[currentSlot]);
        mag[currentSlot] += take;
        reserve[currentSlot] -= take;
        AmmoChanged?.Invoke();
    }


    // ===== ОГОНЬ =====
    void HandleFire()
    {
        if (cooldown > 0f || equipTimer > 0f || reloading) return;

        bool trigger = Config.automatic ? input.FireHeld : input.FirePressed;
        if (!trigger) return;

        if (mag[currentSlot] <= 0)
        {
            cooldown = 0.3f; // чтобы сухие щелчки не строчили очередью
            PlaySfx(Config.dryFireSound);
            TryReload(); // магазин пуст — перезаряжаемся автоматически
            return;
        }

        Fire();
    }

    void Fire()
    {
        cooldown = Config.fireInterval;
        mag[currentSlot]--;
        AmmoChanged?.Invoke();

        // Обработка спрей-паттерна
        if (Time.time - lastShotTime > 0.5f) shotsFiredInBurst = 0;
        lastShotTime = Time.time;

        Vector2 patternOffset = Vector2.zero;
        if (Config.sprayPattern != null && shotsFiredInBurst < Config.sprayPattern.Length)
        {
            patternOffset = Config.sprayPattern[shotsFiredInBurst];
        }
        else if (Config.sprayPattern != null)
        {
            // После конца паттерна используем последний элемент + рандом
            patternOffset = Config.sprayPattern[Config.sprayPattern.Length - 1] + UnityEngine.Random.insideUnitCircle * 0.1f;
        }
        shotsFiredInBurst++;

        // Направление: паттерн + случайный разброс (bloom)
        float spreadDeg = Config.baseSpread + bloom;
        Vector2 randomSpread = UnityEngine.Random.insideUnitCircle * spreadDeg;
        Vector3 dir = eyes.transform.rotation * Quaternion.Euler(patternOffset.y + randomSpread.y, patternOffset.x + randomSpread.x, 0f) * Vector3.forward;
        Vector3 origin = eyes.transform.position;

        // Хитскан: ближайшее попадание, пропуская коллайдеры самого игрока
        Vector3 endPoint = origin + dir * Config.range;
        int count = Physics.RaycastNonAlloc(new Ray(origin, dir), hits, Config.range, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, 0, count, comparer);

        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;

            RaycastHit hit = hits[i];
            endPoint = hit.point;

            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsAlive)
            {
                var hitbox = hit.collider.GetComponentInParent<Hitbox>();
                float dmg = Config.damage * (hitbox != null ? hitbox.multiplier : 1f);
                health.TakeDamage(dmg, hit.point, dir);
                HitConfirmed?.Invoke(dmg, !health.IsAlive);
            }
            else
            {
                effects.PlayImpact(hit, Config);
            }
            break;
        }

        // Эффекты выстрела через WeaponEffects
        effects.PlayMuzzleFlash(flash);
        effects.PlayTracer(muzzle != null ? muzzle.position : eyes.transform.position, endPoint, Config);
        if (Config.fireSound != null) AudioSource.PlayClipAtPoint(Config.fireSound, muzzle != null ? muzzle.position : eyes.transform.position, 0.9f);

        // Разброс и отдача растут
        bloom = Mathf.Min(bloom + Config.spreadPerShot, Config.maxSpread);
        BloomChanged?.Invoke();

        var cam = GetComponentInChildren<PlayerCamera>();
        if (cam != null) cam.AddRecoil(Config.recoilPitch, Config.recoilYaw);

        var vmAnim = viewModels[currentSlot] != null ? viewModels[currentSlot].GetComponent<WeaponViewModel>() : null;
        if (vmAnim != null)
        {
            vmAnim.Kick(0.05f, 4f);
            vmAnim.CycleSlide();
        }
    }

    // 2D-щелчки с лёгкой вариацией питча, чтобы не звучали роботом
    void PlaySfx(AudioClip clip, float volume = 0.85f)
    {
        if (clip == null || sfx == null) return;
        sfx.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        sfx.PlayOneShot(clip, volume);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    class DistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
