using UnityEngine;

/// <summary>
/// Движение игрока в стиле CS2 (Source Engine): мгновенный разгон/торможение
/// на земле и честный air strafing в воздухе.
/// Важно: пивот капсулы — у ног, т.е. transform.position = точка под ногами.
/// Это упрощает спавн по точкам и синхронизацию позиции по сети.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(10)] // после PlayerCamera: движение считается по уже повёрнутому телу
public class PlayerController : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Обработчик ввода (PlayerInputHandler)")]
    public PlayerInputHandler input;
    [Tooltip("Корень камеры (CameraRoot). Здесь меняется ТОЛЬКО высота глаз — наклон у самой камеры, оружие у WeaponHolder")]
    public Transform cameraRoot;

    [Header("Скорости (м/с, как в CS2)")]
    [Tooltip("Обычная скорость бега")]
    public float walkSpeed = 6.5f;
    [Tooltip("Скорость в присяде")]
    public float crouchSpeed = 3.0f;

    [Header("Ускорения")]
    [Tooltip("Как быстро игрок разгоняется/тормозит на земле")]
    public float groundAcceleration = 70f;
    [Tooltip("Ускорение в воздухе вдоль wish-направления (аналог sv_airaccelerate)")]
    public float airAcceleration = 10f;
    [Tooltip("Потолок прироста скорости вдоль wish-направления в воздухе. В CS2 это 30 юнитов/с (примерно 0.8 м/с в нашем масштабе). Именно из-за этого потолка strafe-поворот даёт разгон выше беговой скорости")]
    public float airSpeedCap = 0.8f;

    [Header("Прыжок и гравитация")]
    public float jumpHeight = 1.0f;
    public float gravity = -20f;

    [Header("Присед")]
    public float standingHeight = 2f;
    public float crouchHeight = 1.4f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("Вкл: присед по нажатию (переключатель). Выкл: присед по удержанию (как в CS2)")]
    public bool toggleCrouch = true;
    [Tooltip("Глаза = верхняя точка капсулы минус этот отступ")]
    public float eyeOffset = 0.2f;

    [Header("Проверка земли")]
    public Transform groundCheck;
    public float groundDistance = 0.3f;
    public LayerMask groundMask;

    // ===== Состояние =====
    private CharacterController controller;
    private Vector3 horizontalVelocity;   // горизонтальная скорость — здесь живёт air strafe
    private float verticalVelocity;        // вертикальная скорость
    private bool isGrounded;
    private bool isCrouching;
    private float currentHeight;

    private static readonly Collider[] overlapBuffer = new Collider[16];

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (input == null) input = GetComponent<PlayerInputHandler>();
        if (cameraRoot == null) cameraRoot = transform.Find("CameraRoot");

        if (groundCheck == null)
        {
            GameObject go = new GameObject("GroundCheck");
            go.transform.SetParent(transform, false);
            groundCheck = go.transform;
        }
        groundCheck.localPosition = Vector3.zero; // пивот у ног — точка проверки прямо под капсулой

        currentHeight = controller.height > 0.1f ? controller.height : standingHeight;
        ApplyHeight(currentHeight);
    }

    void Update()
    {
        // Если ввода нет (сетевой клон игрока или сцена ещё не собрана) — ничего не делаем
        if (input == null) return;

        UpdateGround();
        UpdateCrouch();
        UpdateMovement();
        UpdateJump();
        UpdateGravity();
        ApplyMovement();
        UpdateCameraHeight();
    }

    // ===== ЗЕМЛЯ =====
    void UpdateGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // лёгкий "прижим" к земле, чтобы CharacterController не считал нас падающими
    }

    // ===== ПРИСЕД =====
    void UpdateCrouch()
    {
        if (toggleCrouch)
        {
            if (input.CrouchPressed) isCrouching = !isCrouching;
        }
        else
        {
            isCrouching = input.CrouchHeld;
        }

        // Хотим встать, но сверху препятствие — остаёмся в приседе, пока не освободится место
        if (!isCrouching && IsCeilingBlocked())
            isCrouching = true;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float newHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        ApplyHeight(newHeight);
    }

    // Есть ли геометрия на пути вставания (макушка при полном росте)?
    bool IsCeilingBlocked()
    {
        float radius = Mathf.Max(controller.radius - 0.05f, 0.05f);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        int hits = Physics.OverlapSphereNonAlloc(top, radius, overlapBuffer, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            if (overlapBuffer[i] != null && !overlapBuffer[i].transform.IsChildOf(transform))
                return true;
        }
        return false;
    }

    // Высота капсулы меняется от ног, а не от центра: центр всегда на height/2 над пивотом
    void ApplyHeight(float height)
    {
        currentHeight = height;
        controller.height = height;
        Vector3 center = controller.center;
        center.x = 0f;
        center.z = 0f;
        center.y = height * 0.5f;
        controller.center = center;
    }

    // ===== ДВИЖЕНИЕ =====
    void UpdateMovement()
    {
        // "Желание" игрока — относительно текущего направления тела (его вращает камера)
        Vector2 axis = input.Move;
        Vector3 wishDir = transform.right * axis.x + transform.forward * axis.y;
        wishDir.y = 0f;

        float inputMagnitude = wishDir.magnitude;
        if (inputMagnitude > 0.001f) wishDir /= inputMagnitude;
        else wishDir = Vector3.zero;

        float wishSpeed = inputMagnitude * (isCrouching ? crouchSpeed : walkSpeed);

        if (isGrounded)
            GroundMove(wishDir, wishSpeed);
        else
            AirAccelerate(wishDir, wishSpeed);
    }

    // На земле: плавный разгон/торможение к целевой скорости (тот самый counter-strafing)
    void GroundMove(Vector3 wishDir, float wishSpeed)
    {
        Vector3 target = wishDir * wishSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, groundAcceleration * Time.deltaTime);
    }

    // В воздухе: механика Source Engine (air strafing).
    // Мы НЕ ограничиваем общую скорость — только проекцию на wish-направление,
    // поэтому, поворачивая мышь синхронно со стрейфом, можно разогнаться выше беговой.
    void AirAccelerate(Vector3 wishDir, float wishSpeed)
    {
        if (wishDir == Vector3.zero || wishSpeed < 0.01f) return;

        // Полезная скорость вдоль wishDir ограничена cap'ом (в CS2: 30 юнитов/с)
        float cappedWishSpeed = Mathf.Min(wishSpeed, airSpeedCap);

        // Насколько мы уже движемся вдоль wishDir
        float currentSpeed = Vector3.Dot(horizontalVelocity, wishDir);
        float addSpeed = cappedWishSpeed - currentSpeed;
        if (addSpeed <= 0f) return;

        // Ускорение считается от ПОЛНОЙ желаемой скорости — как в Source
        float accelSpeed = airAcceleration * wishSpeed * Time.deltaTime;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        horizontalVelocity += wishDir * accelSpeed;
    }

    // ===== ПРЫЖОК =====
    void UpdateJump()
    {
        if (input.JumpPressed && isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); // v = sqrt(2gh)
    }

    // ===== ГРАВИТАЦИЯ =====
    void UpdateGravity()
    {
        verticalVelocity += gravity * Time.deltaTime;
    }

    // ===== ФИНАЛЬНОЕ ПРИМЕНЕНИЕ =====
    void ApplyMovement()
    {
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    // ===== КАМЕРА: только высота глаз =====
    void UpdateCameraHeight()
    {
        if (cameraRoot == null) return;
        Vector3 p = cameraRoot.localPosition;
        p.x = 0f;
        p.z = 0f;
        p.y = currentHeight - eyeOffset;
        cameraRoot.localPosition = p;
    }

    // ===== ГЕТТЕРЫ ДЛЯ ДРУГИХ СИСТЕМ (анимации, звуки, оружие, сеть) =====
    public bool IsGrounded() => isGrounded;
    public bool IsCrouching() => isCrouching;
    public float GetHorizontalSpeed() => horizontalVelocity.magnitude;
    public Vector3 GetVelocity() => horizontalVelocity + Vector3.up * verticalVelocity;
    public float GetHeight() => currentHeight;
}
