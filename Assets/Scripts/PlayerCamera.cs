using UnityEngine;

/// <summary>
/// Обзор от первого лица. Pitch (вверх/вниз) — у камеры,
/// yaw (влево/вправо) — у корня игрока.
/// Дельта мыши берётся из нового Input System (сырые пиксели за кадр,
/// без сглаживания — отзывчиво, как в CS2).
/// </summary>
[DefaultExecutionOrder(-10)] // поворачиваем тело до того, как PlayerController посчитает движение
public class PlayerCamera : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Обработчик ввода (обычно на корне игрока)")]
    public PlayerInputHandler input;
    [Tooltip("Корень игрока — вращается влево/вправо")]
    public Transform playerBody;

    [Header("Настройки")]
    [Tooltip("Чувствительность мыши. В новых Input System дельта — в пикселях, поэтому значение здесь примерно в 10 раз меньше, чем в старом Input Manager (было 2.5, стало 0.25)")]
    public float mouseSensitivity = 0.25f;
    [Tooltip("Максимальный угол взгляда вверх/вниз")]
    public float clampAngle = 89f;
    [Tooltip("Инвертировать ось Y")]
    public bool invertY = false;

    [Header("Отдача")]
    [Tooltip("Как быстро камера возвращается после отдачи от выстрела")]
    public float recoilRecovery = 10f;

    // Текущий импульс тряски (градусы)
    private float shakePitch;
    private float shakeYaw;

    private float pitch;

    void Awake()
    {
        if (input == null) input = GetComponentInParent<PlayerInputHandler>();

        if (playerBody == null)
        {
            var controller = GetComponentInParent<PlayerController>();
            if (controller != null) playerBody = controller.transform;
            else playerBody = transform.root;
        }
    }


    void OnEnable()
    {
        LockCursor();
        if (input != null) input.PausePressed += ToggleCursorLock;
    }

    void OnDisable()
    {
        if (input != null) input.PausePressed -= ToggleCursorLock;
    }

    void Update()
    {
        if (input == null) return;

        Vector2 look = input.Look;
        float mouseX = look.x * mouseSensitivity;
        float mouseY = look.y * mouseSensitivity;
        if (invertY) mouseY = -mouseY;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -clampAngle, clampAngle);

        // Тряска плавно возвращается к нулю
        shakePitch = Mathf.Lerp(shakePitch, 0f, 1f - Mathf.Exp(-recoilRecovery * 2f * Time.deltaTime));
        shakeYaw = Mathf.Lerp(shakeYaw, 0f, 1f - Mathf.Exp(-recoilRecovery * 2f * Time.deltaTime));

        transform.localRotation = Quaternion.Euler(pitch + shakePitch, shakeYaw, 0f);

        if (playerBody != null && mouseX != 0)
            playerBody.Rotate(Vector3.up * mouseX);
    }


    public void AddRecoil(float pitchDegrees, float yawDegrees)
    {
        // Теперь это только визуальный импульс, он не меняет основной поворот игрока/камеры
        shakePitch += pitchDegrees * 0.4f;
        shakeYaw += yawDegrees * 0.4f;
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            LockCursor();
        }
    }
}
