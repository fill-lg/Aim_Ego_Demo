using UnityEngine;

/// <summary>
/// Оживляет вьюмодель оружия: инерция при повороте мыши (sway),
/// покачивание при ходьбе (bob), отдача (kick), ход затвора при выстреле
/// и процедурная перезарядка по этапам.
/// </summary>
public class WeaponViewModel : MonoBehaviour
{
    public const float TMagOut = 0.15f;
    public const float TMagIn = 0.55f;
    public const float TRack = 0.78f;
    public const float TRackRelease = 0.90f;

    [Header("Ссылки (заполняет PlayerShooting при спавне)")]
    public PlayerInputHandler input;
    public PlayerShooting shooting;
    public PlayerController controller;
    public Animator animator;

    [Header("Инерция от мыши")]
    public float swayAmount = 0.004f;
    public float swaySharpness = 8f;
    public float swayMax = 0.03f;

    [Header("Процедурный Bobbing")]
    public float walkBobFreq = 9f;
    public float walkBobAmp = 0.015f;
    public float sprintBobFreq = 12f;
    public float sprintBobAmp = 0.025f;

    [Header("Эффекты движения")]
    public float leanAmount = 2f;
    public float leanSpeed = 5f;
    public float landingDipAmount = 0.1f;
    public float landingDipSpeed = 10f;

    [Header("Отдача")]
    public float kickRecovery = 12f;

    [Header("Доставание (Equip)")]
    public Vector3 equipStartOffset = new Vector3(0.2f, -0.4f, -0.2f);
    public Vector3 equipStartRot = new Vector3(10f, 0f, -10f);
    public float equipSpeed = 6f;

    [Header("Затвор")]
    public Transform slidePart;
    public Vector3 slideBackLocal;
    public float slideReturnSpeed = 22f;

    [Header("Магазин")]
    public Transform magPart;
    public Vector3 magOutLocal;

    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector2 sway;
    private float bobT;
    private Vector3 kick;
    private float kickRot;
    private Vector3 slideBase;
    private float slideOffset;
    private Vector3 magBase;
    private float poseSmooth;
    private float landingDip;
    private float currentLean;

    private float equipProgress = 1f;
    private Vector3 currentEquipOffset;
    private Vector3 currentEquipRot;

    private float adsAmount = 0f;
    private Vector3 currentAdsOffset;
    private Vector3 currentAdsRot;

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        if (slidePart != null) slideBase = slidePart.localPosition;
        if (magPart != null) magBase = magPart.localPosition;
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null) return;
        if (controller != null)
        {
            animator.SetBool("isSprinting", controller.IsSprinting());
            animator.SetBool("isCrouching", controller.IsCrouching());
            animator.SetBool("isJumping", controller.IsJumping());
            animator.SetFloat("speed", controller.GetHorizontalSpeed());
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateAnimationParameters();

        // 1. Equip
        if (equipProgress < 1f)
        {
            equipProgress += dt * equipSpeed;
            float t = Mathf.SmoothStep(0f, 1f, equipProgress);
            currentEquipOffset = Vector3.Lerp(equipStartOffset, Vector3.zero, t);
            currentEquipRot = Vector3.Lerp(equipStartRot, Vector3.zero, t);
        }

        // 2. ADS
        if (input != null)
        {
            float targetAds = input.AimHeld ? 1f : 0f;
            float speed = (shooting != null && shooting.Config != null) ? shooting.Config.adsTransitionSpeed : 10f;
            adsAmount = Mathf.MoveTowards(adsAmount, targetAds, dt * speed);
            if (shooting != null && shooting.Config != null)
            {
                currentAdsOffset = Vector3.Lerp(Vector3.zero, shooting.Config.adsPosition, adsAmount);
                currentAdsRot = Vector3.Lerp(Vector3.zero, shooting.Config.adsRotation, adsAmount);
            }
        }

        // 3. Sway
        if (input != null)
        {
            Vector2 look = input.Look;
            sway += new Vector2(-look.x, look.y) * swayAmount;
        }
        sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-swaySharpness * dt));
        sway = Vector2.ClampMagnitude(sway, swayMax);

        // 4. Procedural Bobbing
        float moveSpeed = 0f;
        bool isSprinting = false;
        if (controller != null)
        {
            moveSpeed = controller.GetHorizontalSpeed();
            isSprinting = controller.IsSprinting();
        }
        float currentFreq = isSprinting ? sprintBobFreq : walkBobFreq;
        float currentAmp = isSprinting ? sprintBobAmp : walkBobAmp;
        float bobMult = Mathf.Clamp01(moveSpeed / 6.5f);
        bobT += dt * currentFreq * bobMult;
        Vector3 bob = new Vector3(
            Mathf.Cos(bobT * 0.5f) * currentAmp * bobMult,
            Mathf.Sin(bobT) * currentAmp * bobMult,
            0f);

        // 5. Landing Dip & Jump
        if (controller != null)
        {
            if (controller.IsGrounded())
                landingDip = Mathf.MoveTowards(landingDip, 0f, dt * landingDipSpeed);
            else if (controller.IsJumping())
                landingDip = -0.05f;
        }

        // 6. Strafe Lean
        float targetLean = 0f;
        if (input != null) targetLean = -input.Move.x * leanAmount;
        currentLean = Mathf.Lerp(currentLean, targetLean, dt * leanSpeed);

        // 7. Kick
        kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-kickRecovery * dt));
        kickRot = Mathf.Lerp(kickRot, 0f, 1f - Mathf.Exp(-kickRecovery * dt));

        // 8. Reload
        float poseTarget = 0f, magOutVal = 0f, rack = 0f;
        if (shooting != null && shooting.IsReloading)
        {
            float p = shooting.ReloadProgress;
            poseTarget = Envelope(p, 0.10f, 0.90f);
            magOutVal = Ramp(p, TMagOut, 0.12f) * (1f - Ramp(p, TMagIn, 0.12f));
            rack = p < TRackRelease ? Ramp(p, TRack, TRackRelease - TRack) : 0f;
        }
        poseSmooth = Mathf.MoveTowards(poseSmooth, poseTarget, 6f * dt);

        // FINAL POSITION
        transform.localPosition = basePos
            + currentEquipOffset
            + currentAdsOffset
            + new Vector3(sway.x, sway.y, 0f)
            + bob
            + kick
            + Vector3.down * (0.16f * poseSmooth + landingDip)
            + Vector3.back * (0.05f * poseSmooth);

        transform.localRotation = baseRot
            * Quaternion.Euler(currentEquipRot.x + currentAdsRot.x + -kickRot - 40f * poseSmooth,
                               currentAdsRot.y + currentLean,
                               18f * poseSmooth + currentAdsRot.z);

        if (shooting != null && shooting.IsReloading)
            slideOffset = rack;
        else
            slideOffset = Mathf.Lerp(slideOffset, 0f, 1f - Mathf.Exp(-slideReturnSpeed * dt));

        if (slidePart != null) slidePart.localPosition = slideBase + slideBackLocal * slideOffset;
        if (magPart != null) magPart.localPosition = magBase + magOutLocal * magOutVal;
    }

    public void Kick(float back = 0.05f, float up = 4f)
    {
        kick += Vector3.back * back + Vector3.up * (back * 0.3f);
        kickRot += up;
    }

    public void CycleSlide() => slideOffset = 1f;
    public void Equip() => equipProgress = 0f;
    public void Attack() => StartCoroutine(MeleeSwing());

    private System.Collections.IEnumerator MeleeSwing()
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 15f;
            kick = Vector3.forward * 0.1f * Mathf.Sin(t * Mathf.PI);
            kickRot = 20f * Mathf.Sin(t * Mathf.PI);
            yield return null;
        }
        kick = Vector3.zero;
        kickRot = 0f;
    }

    static float Ramp(float p, float start, float len)
        => len <= 0f ? (p >= start ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - start) / len));

    static float Envelope(float p, float fadeIn, float fadeOut)
        => Ramp(p, 0f, fadeIn) * (1f - Ramp(p, fadeOut, 1f - fadeOut));
}
