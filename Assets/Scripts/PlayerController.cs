using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера, чтобы движение шло туда, куда смотрит игрок")]
    public Transform cameraTransform;
    
    private CharacterController controller;

    [Header("Скорости (м/с, как в CS2)")]
    [Tooltip("Обычная скорость бега")]
    public float walkSpeed = 6.5f;
    [Tooltip("Скорость в присяде")]
    public float crouchSpeed = 3.0f;
    [Tooltip("Максимальная скорость в воздухе (ограничение)")]
    public float airMaxSpeed = 30f; // в CS2 можно разогнаться сильно за счёт strafing
    
    [Header("Ускорения")]
    [Tooltip("Как быстро игрок разгоняется/тормозит на земле")]
    public float groundAcceleration = 70f;
    [Tooltip("Управление в воздухе (sv_airaccelerate в CS2 = 12, но для Unity берём больше)")]
    public float airAcceleration = 12f;
    
    [Header("Прыжок и гравитация")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    
    [Header("Присед")]
    public float standingHeight = 2f;
    public float crouchHeight = 1.2f;
    public float crouchTransitionSpeed = 10f;
    
    [Header("Проверка земли")]
    public Transform groundCheck;
    public float groundDistance = 0.3f;
    public LayerMask groundMask;

    // Состояние
    private Vector3 velocity;          // текущая скорость (включая гравитацию)
    private Vector3 horizontalVelocity;// только горизонтальная скорость
    private bool isGrounded;
    private bool isCrouching;
    private float currentHeight;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentHeight = standingHeight;
        controller.height = standingHeight;
        
        // Если groundCheck не задан — создаём точку под ногами
        if (groundCheck == null)
        {
            GameObject go = new GameObject("GroundCheck");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, -controller.height / 2f, 0);
            groundCheck = go.transform;
        }
    }

    void Update()
    {
        CheckGround();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        ApplyFinalMovement();
    }

    // ===== ПРОВЕРКА ЗЕМЛИ =====
    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        // Если стоим на земле и падали — обнуляем вертикальную скорость
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // небольшое значение, чтобы CharacterController считал, что мы на земле
        }
    }

    // ===== ПРИСЕД =====
    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.height = currentHeight;
        
        // Сдвигаем камеру, чтобы она не "уехала" в пол при присяде
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                currentHeight - 0.2f, // 0.2 — отступ от макушки до глаз
                cameraTransform.localPosition.z
            );
        }
    }

    // ===== ДВИЖЕНИЕ =====
    void HandleMovement()
    {
        // Направление "хотения" — относительно того, куда смотрит камера (проекция на горизонт)
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 inputDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) inputDir += camForward;
        if (Input.GetKey(KeyCode.S)) inputDir -= camForward;
        if (Input.GetKey(KeyCode.D)) inputDir += camRight;
        if (Input.GetKey(KeyCode.A)) inputDir -= camRight;
        
        if (inputDir.magnitude > 1f) inputDir.Normalize();

        float targetSpeed = isCrouching ? crouchSpeed : walkSpeed;

        if (isGrounded)
        {
            // На земле: плавное ускорение/торможение (тот самый counter-strafing)
            Vector3 targetHorizontal = inputDir * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity, 
                targetHorizontal, 
                groundAcceleration * Time.deltaTime
            );
        }
        else
        {
            // В ВОЗДУХЕ: механика Source Engine (air strafing)
            // Мы НЕ ограничиваем скорость жёстко, а добавляем ускорение в сторону "желания"
            ApplyAirAcceleration(inputDir, targetSpeed);
        }
    }

    // ===== AIR STRAFING (сердце механики CS2) =====
    void ApplyAirAcceleration(Vector3 wishDir, float wishSpeed)
    {
        if (wishDir == Vector3.zero) return;

        float currentSpeed = horizontalVelocity.magnitude;
        
        // Добавляем скорость в направлении wishDir
        Vector3 addSpeed = wishDir * (airAcceleration * wishSpeed * Time.deltaTime);
        Vector3 newVelocity = horizontalVelocity + addSpeed;
        
        // Ограничиваем проекцию новой скорости на wishDir значением wishSpeed
        // Это позволяет разгоняться, поворачиваясь в сторону движения (strafe jump)
        float newSpeed = newVelocity.magnitude;
        if (newSpeed > wishSpeed)
        {
            // Если превысили — проецируем обратно на окружность
            float dot = Vector3.Dot(newVelocity, wishDir);
            if (dot > wishSpeed)
            {
                newVelocity = wishDir * wishSpeed + (newVelocity - wishDir * dot);
            }
        }
        
        horizontalVelocity = newVelocity;
    }

    // ===== ПРЫЖОК =====
    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // v = sqrt(2 * g * h)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // ===== ГРАВИТАЦИЯ =====
    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }

    // ===== ФИНАЛЬНОЕ ПРИМЕНЕНИЕ =====
    void ApplyFinalMovement()
    {
        // Собираем итоговый вектор: горизонталь + вертикаль
        Vector3 move = horizontalVelocity + Vector3.up * velocity.y;
        controller.Move(move * Time.deltaTime);
    }

    // ===== ГЕТТЕРЫ ДЛЯ ДРУГИХ СИСТЕМ (анимации, звуки, оружие) =====
    public bool IsGrounded() => isGrounded;
    public bool IsCrouching() => isCrouching;
    public float GetHorizontalSpeed() => horizontalVelocity.magnitude;
    public Vector3 GetVelocity() => horizontalVelocity + Vector3.up * velocity.y;
}