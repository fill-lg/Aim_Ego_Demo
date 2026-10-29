using UnityEngine;

/// <summary>
/// Связующее звено между физикой движения (PlayerController) и визуализацией (Animator).
/// Обновляет параметры аниматора на основе текущего состояния игрока.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerController controller;
    public PlayerInputHandler input;
    public PlayerShooting shooting;

    private Animator animator;

    // Кэшированные ID параметров для оптимизации (быстрее, чем строки)
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");
    private static readonly int IsCrouchingHash = Animator.StringToHash("isCrouching");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");

    void Awake()
    {
        animator = GetComponent<Animator>();

        if (controller == null) controller = GetComponent<PlayerController>();
        if (input == null) input = GetComponent<PlayerInputHandler>();
        if (shooting == null) shooting = GetComponent<PlayerShooting>();
    }

    private bool wasGroundedLastFrame;

    void Update()
    {
        if (animator == null || controller == null) return;

        UpdateMovementAnimations();
        UpdateStateAnimations();
        wasGroundedLastFrame = controller.IsGrounded();
    }

    private void UpdateMovementAnimations()
    {
        float currentSpeed = controller.GetHorizontalSpeed();
        float maxSpeed = controller.walkSpeed;
        float normalizedSpeed = Mathf.Clamp01(currentSpeed / maxSpeed);
        animator.SetFloat(SpeedHash, normalizedSpeed);
    }

    private void UpdateStateAnimations()
    {
        bool grounded = controller.IsGrounded();
        animator.SetBool(IsGroundedHash, grounded);

        if (shooting != null)
        {
            animator.SetInteger(WeaponTypeHash, shooting.CurrentSlot);
        }

        // Логика прыжка: срабатывает в момент, когда мы только что оторвались от земли
        if (wasGroundedLastFrame && !grounded)
        {
            Debug.Log("AnimController: Triggering Jump!");
            animator.SetTrigger(JumpTriggerHash);
        }
    }
}
