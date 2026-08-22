using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("Настройки чувствительности")]
    [Tooltip("Чувствительность мыши. В CS2 аналогично настраивается в консоли (sensitivity)")]
    public float mouseSensitivity = 2.5f; 
    
    [Header("Оси и ограничения")]
    [Tooltip("Ссылка на объект игрока (тело/капсулу), чтобы вращать его влево/вправо")]
    public Transform playerBody; 
    
    [Tooltip("Максимальный угол взгляда вверх/вниз")]
    public float clampAngle = 89f; 
    
    [Tooltip("Инвертировать ось Y (как в старых играх)")]
    public bool invertY = false;

    // Внутренняя переменная для хранения угла наклона камеры (Pitch)
    private float xRotation = 0f;

    void Start()
    {
        // Блокируем и скрываем курсор при старте
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Проверка на случай, если вы забыли назначить playerBody в инспекторе
        if (playerBody == null)
        {
            Debug.LogWarning("CS2_CameraLook: Не указан playerBody! Камера не будет вращаться по горизонтали.");
        }
    }

    void Update()
    {
        // Используем GetAxisRaw, чтобы отключить встроенное сглаживание Unity.
        // Это дает тот самый "сырой" и отзывчивый ввод как в CS2.
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (invertY) mouseY = -mouseY;

        // 1. Вращение камеры ВВЕРХ/ВНИЗ (Pitch / Ось X)
        xRotation -= mouseY;
        // Ограничиваем угол, чтобы камера не перевернулась через голову
        xRotation = Mathf.Clamp(xRotation, -clampAngle, clampAngle);

        // Применяем вращение только к локальной оси X самой камеры
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 2. Вращение игрока ВЛЕВО/ВПРАВО (Yaw / Ось Y)
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}