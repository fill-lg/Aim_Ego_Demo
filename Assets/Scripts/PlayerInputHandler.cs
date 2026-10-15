using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Единая точка ввода игрока (новый Input System).
/// Остальные скрипты читают ввод только отсюда — так его проще
/// отключать (пауза, меню) и включать только для владельца при сетевой игре.
/// Ассет PlayerControls.inputactions создаётся через AimEgo > Setup (Фаза 0).
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    [Tooltip("Ассет ввода PlayerControls.inputactions")]
    public InputActionAsset actionsAsset;

    private InputAction move, look, jump, crouch, fire, reload, slot1, slot2, quickSwap, pause;

    /// <summary>Событие: нажат Esc. Пока используется только для снятия захвата курсора, в фазе UI — для меню паузы.</summary>
    public event Action PausePressed;

    // ===== Поллинг для игрового цикла (null-безопасно: пока ввод не настроен, отдаём нули) =====

    /// <summary>x — вправо/влево, y — вперёд/назад</summary>
    public Vector2 Move => _cachedMove;

    /// <summary>Сырая дельта мыши за кадр, в пикселях</summary>
    public Vector2 Look => _cachedLook;

    public bool JumpPressed    => jump != null && jump.WasPressedThisFrame();
    public bool JumpHeld       => jump != null && jump.IsPressed();
    public bool CrouchPressed  => crouch != null && crouch.WasPressedThisFrame();
    public bool CrouchHeld     => crouch != null && crouch.IsPressed();
    public bool FirePressed    => fire != null && fire.WasPressedThisFrame();
    public bool FireHeld       => fire != null && fire.IsPressed();
    public bool ReloadPressed  => reload != null && reload.WasPressedThisFrame();
    public bool Slot1Pressed   => slot1 != null && slot1.WasPressedThisFrame();
    public bool Slot2Pressed   => slot2 != null && slot2.WasPressedThisFrame();
    public bool QuickSwapPressed => quickSwap != null && quickSwap.WasPressedThisFrame();

    private Vector2 _cachedMove;
    private Vector2 _cachedLook;

    void Update()
    {
        // Кэшируем значения ввода один раз за кадр для оптимизации
        _cachedMove = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        _cachedLook = look != null ? look.ReadValue<Vector2>() : Vector2.zero;
    }

    void Awake()
    {
        if (actionsAsset == null)
        {
            Debug.LogError("PlayerInputHandler: не назначен ассет Input Actions. " +
                           "Запусти AimEgo > Setup > Фаза 0 (создаёт Assets/Settings/PlayerControls.inputactions).", this);
            return;
        }

        move   = actionsAsset.FindAction("Player/Move", true);
        look   = actionsAsset.FindAction("Player/Look", true);
        jump   = actionsAsset.FindAction("Player/Jump", true);
        crouch = actionsAsset.FindAction("Player/Crouch", true);
        fire   = actionsAsset.FindAction("Player/Fire", true);
        reload = actionsAsset.FindAction("Player/Reload", true);
        slot1  = actionsAsset.FindAction("Player/Slot1", true);
        slot2  = actionsAsset.FindAction("Player/Slot2", true);
        // QuickSwap появился позже — в старых ассетах его может не быть, потому без throwOnMissing
        quickSwap = actionsAsset.FindAction("Player/QuickSwap", false);
        pause  = actionsAsset.FindAction("UI/Pause", true);

        if (pause != null) pause.performed += OnPause;
    }

    void OnDestroy()
    {
        if (pause != null) pause.performed -= OnPause;
    }

    void OnEnable()
    {
        actionsAsset?.Enable();
        // Гарантируем, что кэш сброшен при включении
        _cachedMove = Vector2.zero;
        _cachedLook = Vector2.zero;
    }

    void OnDisable() => actionsAsset?.Disable();


    private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
}
