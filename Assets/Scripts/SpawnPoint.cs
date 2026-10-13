using UnityEngine;

/// <summary>
/// Точка появления игрока.
/// Спавны — это контейнеры пропсов карты, поэтому их собственный поворот
/// и высота (y=0) не используются: игрок встаёт лицом к центру карты,
/// а позиция ног ищется рейкастом вниз до поверхности пола — контейнеры
/// стоят на нуле, а сам пол (кубы-тайлы) выше.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    public enum Team { CT, T }

    public Team team;

    [Tooltip("Слои, которые считаются полом при поиске поверхности под точкой спавна")]
    public LayerMask groundMask = 1 << 3; // Ground

    [Tooltip("Насколько выше поверхности пола ставить ноги (запас от неровностей)")]
    public float surfaceOffset = 0.1f;

    /// <summary>Позиция ног игрока: пол ищется рейкастом сверху вниз.</summary>
    public Vector3 Position
    {
        get
        {
            Vector3 origin = transform.position + Vector3.up * 15f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * surfaceOffset;

            // Пол не нашли (нет коллайдеров на слоях groundMask) — ставим над точкой
            // и даём гравитации довести игрока до земли
            return transform.position + Vector3.up * 2f;
        }
    }

    /// <summary>Поворот игрока лицом к центру карты.</summary>
    public Quaternion Rotation()
    {
        Vector3 toCenter = Vector3.ProjectOnPlane(-transform.position, Vector3.up);
        if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
        return Quaternion.LookRotation(toCenter.normalized, Vector3.up);
    }

    void OnDrawGizmos()
    {
        Vector3 pos = Position;
        Gizmos.color = team == Team.CT ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.55f, 0.2f);
        Gizmos.DrawWireCube(pos + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        Gizmos.DrawLine(pos, pos + Vector3.up * 1.8f);
        Gizmos.DrawRay(pos + Vector3.up * 0.9f, Rotation() * Vector3.forward * 1.5f);
    }
}
