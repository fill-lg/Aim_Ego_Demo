using UnityEngine;

/// <summary>
/// Зона попадания на теле. Навешивается на коллайдеры (голова, тело),
/// урон умножается на multiplier. Владельца ищет PlayerHealth выше по иерархии.
/// </summary>
public class Hitbox : MonoBehaviour
{
    [Tooltip("Множитель урона этой зоны (голова = 4)")]
    public float multiplier = 1f;
}
