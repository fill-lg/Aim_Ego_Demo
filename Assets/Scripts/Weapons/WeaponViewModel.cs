using UnityEngine;

/// <summary>
/// Оживляет вьюмодель оружия: инерция при повороте мыши (sway),
/// покачивание при ходьбе (bob), отдача (kick), ход затвора при выстреле
/// и процедурная перезарядка по этапам: наклон → магазин наружу →
/// магазин вставлен → передёргивание затвора → возврат.
/// Всё процедурно, без аниматоров — создаётся в префабе вьюмодели.
/// </summary>
public class WeaponViewModel : MonoBehaviour
{
    // Ключевые точки таймлайна перезарядки, доля от ReloadProgress (0..1).
    // По ним же PlayerShooting играет звуки этапов.
    public const float TMagOut = 0.15f;
    public const float TMagIn = 0.55f;
    public const float TRack = 0.78f;
    public const float TRackRelease = 0.90f;

    [Header("Ссылки (заполняет PlayerShooting при спавне)")]
    public PlayerInputHandler input;
    public PlayerShooting shooting;
    public PlayerController controller;

    [Header("Инерция от мыши")]
    public float swayAmount = 0.004f;
    public float swaySharpness = 8f;
    public float swayMax = 0.03f;

    [Header("Покачивание при ходьбе")]
    public float bobFrequency = 9f;
    public float bobAmplitude = 0.012f;

    [Header("Отдача")]
    public float kickRecovery = 12f;

    [Header("Затвор (заполняет сетап-скрипт)")]
    [Tooltip("Часть, ходящая назад при выстреле: Bolt у AK, сам Model у цельных моделей")]
    public Transform slidePart;
    [Tooltip("Куда смещается затвор при полном отходе (в локальных осях родителя)")]
    public Vector3 slideBackLocal;
    [Tooltip("Скорость возврата затвора вперёд")]
    public float slideReturnSpeed = 22f;

    [Header("Магазин (заполняет сетап-скрипт)")]
    [Tooltip("Меш магазина, если модель разбита на части (AK). У цельных — пусто")]
    public Transform magPart;
    [Tooltip("Смещение магазина при перезарядке (в локальных осях родителя)")]
    public Vector3 magOutLocal;

    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector2 sway;
    private float bobT;
    private Vector3 kick;
    private float kickRot;
    private Vector3 slideBase;
    private float slideOffset;   // 0..1 — насколько отведён затвор
    private Vector3 magBase;
    private float poseSmooth;    // наклон при перезарядке, сглаженный

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        if (slidePart != null) slideBase = slidePart.localPosition;
        if (magPart != null) magBase = magPart.localPosition;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Инерция: оружие чуть отстаёт от поворота камеры
        if (input != null)
        {
            Vector2 look = input.Look;
            sway += new Vector2(-look.x, look.y) * swayAmount;
        }
        sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-swaySharpness * dt));
        sway = Vector2.ClampMagnitude(sway, swayMax);

        // Покачивание только на земле и в движении
        float f = 0f;
        if (controller != null && controller.IsGrounded())
            f = Mathf.Clamp01(controller.GetHorizontalSpeed() / 6.5f);
        bobT += dt * bobFrequency * f;
        Vector3 bob = new Vector3(
            Mathf.Sin(bobT) * bobAmplitude * f,
            Mathf.Sin(bobT * 2f) * bobAmplitude * 0.7f * f,
            0f);

        // Отдача пружиной возвращается в ноль
        kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-kickRecovery * dt));
        kickRot = Mathf.Lerp(kickRot, 0f, 1f - Mathf.Exp(-kickRecovery * dt));

        // Этапы перезарядки по прогрессу 0..1
        float poseTarget = 0f, magOut = 0f, rack = 0f;
        if (shooting != null && shooting.IsReloading)
        {
            float p = shooting.ReloadProgress;
            poseTarget = Envelope(p, 0.10f, 0.90f);                                  // наклон: вход-плато-выход
            magOut = Ramp(p, TMagOut, 0.12f) * (1f - Ramp(p, TMagIn, 0.12f));        // магазин наружу и обратно
            rack = p < TRackRelease ? Ramp(p, TRack, TRackRelease - TRack) : 0f;    // затвор назад, в конце — резкий отпуск
        }

        // Плавный возврат позы (в т.ч. после отмены перезарядки свапом)
        poseSmooth = Mathf.MoveTowards(poseSmooth, poseTarget, 6f * dt);

        transform.localPosition = basePos
            + new Vector3(sway.x, sway.y, 0f)
            + bob
            + kick
            + Vector3.down * (0.16f * poseSmooth)
            + Vector3.back * (0.05f * poseSmooth);
        transform.localRotation = baseRot * Quaternion.Euler(-kickRot - 40f * poseSmooth, 0f, 18f * poseSmooth);

        // Затвор: во время перезарядки — по таймлайну, иначе импульс от выстрела гасится пружиной
        if (shooting != null && shooting.IsReloading)
            slideOffset = rack;
        else
            slideOffset = Mathf.Lerp(slideOffset, 0f, 1f - Mathf.Exp(-slideReturnSpeed * dt));
        if (slidePart != null)
            slidePart.localPosition = slideBase + slideBackLocal * slideOffset;

        if (magPart != null)
            magPart.localPosition = magBase + magOutLocal * magOut;
    }

    /// Толчок от выстрела: назад к камере и дулом вверх.
    public void Kick(float back = 0.05f, float up = 4f)
    {
        kick += Vector3.back * back + Vector3.up * (back * 0.3f);
        kickRot += up;
    }

    /// Выстрел: затвор резко уходит назад, возврат — пружиной в Update.
    public void CycleSlide()
    {
        slideOffset = 1f;
    }

    // p в [0..1]: 0 до start, 1 после start+len, между — плавно
    static float Ramp(float p, float start, float len)
        => len <= 0f ? (p >= start ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - start) / len));

    // 0 → 1 → 0 с плоской вершиной: быстрый вход до fadeIn, плато, спад с fadeOut
    static float Envelope(float p, float fadeIn, float fadeOut)
        => Ramp(p, 0f, fadeIn) * (1f - Ramp(p, fadeOut, 1f - fadeOut));
}
