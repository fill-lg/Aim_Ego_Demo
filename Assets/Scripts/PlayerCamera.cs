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

    // Текущий подброс от отдачи (градусы, >0 — вверх)
    private float kickPitch;

    private float pitch;

    void Awake()
    {
        if (input == null) input = GetComponentInParent<PlayerInputHandler>();
        if (playerBody == null) playerBody = transform.root;
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

        // 1. Pitch — вверх/вниз, только локальный наклон камеры
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -clampAngle, clampAngle);

        // Отдача от выстрелов плавно возвращается к нулю
        kickPitch = Mathf.Lerp(kickPitch, 0f, 1f - Mathf.Exp(-recoilRecovery * Time.deltaTime));
        transform.localRotation = Quaternion.Euler(pitch - kickPitch, 0f, 0f);

        // 2. Yaw — влево/вправо, вращаем тело игрока
        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);
    }

    /// <summary>
    /// Подброс камеры от выстрела: pitchDegrees вверх, случайный yawDegrees в сторону.
    /// Вызывает PlayerShooting; трасса пули идёт по новой ориентации камеры — как в CS.
    /// </summary>
    public void AddRecoil(float pitchDegrees, float yawDegrees)
    {
        kickPitch += pitchDegrees;
        if (playerBody != null && yawDegrees > 0f)
            playerBody.Rotate(Vector3.up * Random.Range(-yawDegrees, yawDegrees));
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Временное решение до фазы UI: Esc снимает/возвращает захват курсора
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
