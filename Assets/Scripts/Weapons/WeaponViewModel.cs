using UnityEngine;

/// <summary>
/// Оживляет вьюмодель оружия: инерция при повороте мыши (sway),
/// покачивание при ходьбе (bob), отдача (kick) и «просадка» при перезарядке.
/// Всё процедурно, без аниматоров — создаётся в префабе вьюмодели.
/// </summary>
public class WeaponViewModel : MonoBehaviour
{
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

    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector2 sway;
    private float bobT;
    private Vector3 kick;
    private float kickRot;

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
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

        // Перезарядка: оружие опускается и наклоняется
        float dip = 0f;
        if (shooting != null && shooting.IsReloading)
            dip = Mathf.Sin(shooting.ReloadProgress * Mathf.PI);

        transform.localPosition = basePos
            + new Vector3(sway.x, sway.y, 0f)
            + bob
            + kick
            + Vector3.down * (0.18f * dip);
        transform.localRotation = baseRot * Quaternion.Euler(-kickRot - 45f * dip, 0f, 0f);
    }

    /// Толчок от выстрела: назад к камере и дулом вверх.
    public void Kick(float back = 0.05f, float up = 4f)
    {
        kick += Vector3.back * back + Vector3.up * (back * 0.3f);
        kickRot += up;
    }
}
