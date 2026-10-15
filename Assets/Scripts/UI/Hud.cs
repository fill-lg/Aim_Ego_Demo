using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Игровой HUD (uGUI): динамический прицел, хитмаркер, полоска перезарядки,
/// здоровье и панель оружия с патронами и слотами.
/// Объекты создаёт editor-скрипт (Фаза 1.5), здесь только логика обновления.
/// </summary>
public class Hud : MonoBehaviour
{
    [Header("Прицел")]
    public RectTransform[] crosshairBars; // верх, низ, лево, право
    public float crosshairBaseGap = 6f;
    public float crosshairSpreadScale = 14f;

    [Header("Хитмаркер")]
    public CanvasGroup hitmarker;

    [Header("Перезарядка")]
    public GameObject reloadRoot;
    public RectTransform reloadFill;

    [Header("Здоровье")]
    public RectTransform hpFill;
    public Text hpText;

    [Header("Оружие")]
    public Text weaponNameText;
    public Text ammoText;
    public Text[] slotTexts;
    public Color activeSlotColor = Color.white;
    public Color inactiveSlotColor = new Color(1f, 1f, 1f, 0.35f);

    private PlayerShooting shoot;
    private PlayerHealth health;
    private float hitmarkerUntil;
    private int builtSlots = -1;

    void Start()
    {
        shoot = GetComponentInParent<PlayerShooting>();
        health = GetComponentInParent<PlayerHealth>();
        if (shoot != null)
        {
            shoot.HitConfirmed += OnHit;
            shoot.AmmoChanged += UpdateAmmo;
            shoot.WeaponSwitched += UpdateWeaponInfo;
            shoot.ReloadStateChanged += OnReloadStateChanged;
            shoot.BloomChanged += UpdateCrosshair;
        }
        if (health != null)
        {
            health.OnHealthChanged += UpdateHealth;
        }
    }

    void OnDestroy()
    {
        if (shoot != null)
        {
            shoot.HitConfirmed -= OnHit;
            shoot.AmmoChanged -= UpdateAmmo;
            shoot.WeaponSwitched -= UpdateWeaponInfo;
            shoot.ReloadStateChanged -= OnReloadStateChanged;
            shoot.BloomChanged -= UpdateCrosshair;
        }
        if (health != null)
        {
            health.OnHealthChanged -= UpdateHealth;
        }
    }

    void Update()
    {
        if (shoot == null) return;
        if (shoot.Config == null) return;

        float dt = Time.deltaTime;

        // Хитмаркер гаснет за 0.15 с (это таймер, оставляем в Update)
        if (hitmarker != null)
        {
            float left = hitmarkerUntil - Time.time;
            hitmarker.alpha = Mathf.Clamp01(left / 0.15f);
        }

        // Полоска перезарядки (прогресс — непрерывное значение, оставляем в Update)
        if (reloadRoot != null && shoot.IsReloading)
        {
            if (reloadFill != null)
                reloadFill.anchorMax = new Vector2(Mathf.Clamp01(shoot.ReloadProgress), 1f);
        }
    }

    void UpdateAmmo()
    {
        if (ammoText != null && shoot != null)
            ammoText.text = shoot.CurrentMag + " / " + shoot.CurrentReserve;
        UpdateSlots();
    }

    void UpdateWeaponInfo(int slot, WeaponConfig config)
    {
        if (weaponNameText != null && config != null)
            weaponNameText.text = config.displayName;
        UpdateSlots();
    }

    void OnReloadStateChanged(bool isReloading)
    {
        if (reloadRoot != null)
            reloadRoot.SetActive(isReloading);
    }

    void UpdateHealth(float current, float max)
    {
        if (health == null) return;
        float ratio = max > 0f ? current / max : 0f;
        if (hpFill != null) hpFill.anchorMax = new Vector2(ratio, 1f);
        if (hpText != null)
        {
            hpText.text = Mathf.CeilToInt(current).ToString();
            hpText.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), Color.white, ratio);
        }
    }

    void UpdateCrosshair()
    {
        if (shoot == null || shoot.Config == null) return;
        float gap = crosshairBaseGap + (shoot.Config.baseSpread + shoot.Bloom) * crosshairSpreadScale;
        if (crosshairBars != null && crosshairBars.Length == 4)
        {
            SetBar(crosshairBars[0], new Vector2(0f, gap + 4f), new Vector2(2f, 8f));
            SetBar(crosshairBars[1], new Vector2(0f, -gap - 4f), new Vector2(2f, 8f));
            SetBar(crosshairBars[2], new Vector2(-gap - 4f, 0f), new Vector2(8f, 2f));
            SetBar(crosshairBars[3], new Vector2(gap + 4f, 0f), new Vector2(8f, 2f));
        }
    }

    void UpdateSlots()
    {
        if (slotTexts == null || shoot == null || shoot.loadout == null) return;
        if (builtSlots == shoot.CurrentSlot && slotTexts[0] != null && slotTexts[0].text.Length > 0) return;

        for (int i = 0; i < slotTexts.Length && i < shoot.loadout.Length; i++)
        {
            var cfg = shoot.loadout[i];
            if (cfg == null || slotTexts[i] == null) continue;
            slotTexts[i].text = (i + 1) + "   " + cfg.displayName;
            slotTexts[i].color = i == shoot.CurrentSlot ? activeSlotColor : inactiveSlotColor;
        }
        builtSlots = shoot.CurrentSlot;
    }

    static void SetBar(RectTransform bar, Vector2 pos, Vector2 size)
    {
        bar.anchoredPosition = pos;
        bar.sizeDelta = size;
    }

    /// Вызывает PlayerShooting через событие HitConfirmed
    public void OnHit(float damage, bool killed)
    {
        hitmarkerUntil = Time.time + 0.15f;
    }
}
