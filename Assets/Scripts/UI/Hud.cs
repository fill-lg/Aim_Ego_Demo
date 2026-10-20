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
    public float crosshairLineLength = 8f;
    public float crosshairLineThickness = 2f;
    public float crosshairSmoothing = 15f;

    [Header("Хитмаркер")]
    public CanvasGroup hitmarker;
    public AudioClip hitSound;
    public float hitmarkerPopScale = 1.2f;

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
    private float targetCrosshairGap;
    private float currentCrosshairGap;
    private float currentHitmarkerScale = 1f;

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

        float dt = Time.deltaTime;

        // Сглаживание прицела
        if (shoot.Config != null)
        {
            currentCrosshairGap = Mathf.Lerp(currentCrosshairGap, targetCrosshairGap, dt * crosshairSmoothing);
            UpdateCrosshairVisuals();
        }

        // Хитмаркер: затухание и возврат масштаба
        if (hitmarker != null)
        {
            float left = hitmarkerUntil - Time.time;
            hitmarker.alpha = Mathf.Clamp01(left / 0.15f);

            currentHitmarkerScale = Mathf.Lerp(currentHitmarkerScale, 1f, dt * 20f);
            hitmarker.transform.localScale = Vector3.one * currentHitmarkerScale;
        }

        // Полоска перезарядки
        if (reloadRoot != null)
        {
            if (shoot.IsReloading && shoot.Config != null)
            {
                if (reloadFill != null)
                    reloadFill.anchorMax = new Vector2(Mathf.Clamp01(shoot.ReloadProgress), 1f);
            }
            else
            {
                reloadRoot.SetActive(false);
            }
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
        if (weaponNameText != null)
        {
            weaponNameText.text = config != null ? config.displayName : "Пусто";
        }
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
        targetCrosshairGap = crosshairBaseGap + (shoot.Config.baseSpread + shoot.Bloom) * crosshairSpreadScale;
    }

    void UpdateCrosshairVisuals()
    {
        if (crosshairBars == null || crosshairBars.Length != 4) return;

        float offset = currentCrosshairGap + 4f;
        SetBar(crosshairBars[0], new Vector2(0f, offset), new Vector2(crosshairLineThickness, crosshairLineLength));
        SetBar(crosshairBars[1], new Vector2(0f, -offset), new Vector2(crosshairLineThickness, crosshairLineLength));
        SetBar(crosshairBars[2], new Vector2(-offset, 0f), new Vector2(crosshairLineLength, crosshairLineThickness));
        SetBar(crosshairBars[3], new Vector2(offset, 0f), new Vector2(crosshairLineLength, crosshairLineThickness));
    }

    void UpdateSlots()
    {
        if (slotTexts == null || shoot == null || shoot.loadout == null) return;

        for (int i = 0; i < slotTexts.Length && i < shoot.loadout.Length; i++)
        {
            var cfg = shoot.loadout[i];
            if (slotTexts[i] == null) continue;

            if (cfg != null)
            {
                slotTexts[i].text = (i + 1) + "   " + cfg.displayName;
            }
            else
            {
                slotTexts[i].text = (i + 1) + "   ---";
            }
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
        currentHitmarkerScale = hitmarkerPopScale;

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
        }
    }
}
