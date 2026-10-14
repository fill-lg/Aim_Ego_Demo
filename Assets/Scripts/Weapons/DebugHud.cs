using UnityEngine;

/// <summary>
/// Временный HUD (IMGUI) до нормального UI в фазе 4:
/// динамический прицел (расходится от разброса), патроны, имя оружия,
/// полоска перезарядки и хитмаркер.
/// </summary>
[RequireComponent(typeof(PlayerShooting))]
public class DebugHud : MonoBehaviour
{
    private PlayerShooting shoot;
    private float hitmarkerUntil;

    void Start()
    {
        shoot = GetComponent<PlayerShooting>();
        shoot.HitConfirmed += OnHit;
    }

    void OnDestroy()
    {
        if (shoot != null) shoot.HitConfirmed -= OnHit;
    }

    void OnHit(float damage, bool killed)
    {
        hitmarkerUntil = Time.time + 0.15f;
    }

    void OnGUI()
    {
        if (shoot == null || shoot.Config == null) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // Прицел: зазор растёт с разбросом
        float gap = 5f + (shoot.Config.baseSpread + shoot.Bloom) * 16f;
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        DrawRect(cx - 1f, cy - gap - 7f, 2f, 7f);
        DrawRect(cx - 1f, cy + gap, 2f, 7f);
        DrawRect(cx - gap - 7f, cy - 1f, 7f, 2f);
        DrawRect(cx + gap, cy - 1f, 7f, 2f);

        // Хитмаркер
        if (Time.time < hitmarkerUntil)
        {
            GUI.color = Color.red;
            DrawRect(cx - 3f, cy - 3f, 6f, 6f);
        }

        // Оружие и патроны — сверху справа
        GUI.color = prev;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.UpperRight,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(Screen.width - 280f, 14f, 260f, 36f), shoot.Config.displayName, style);
        GUI.Label(new Rect(Screen.width - 280f, 50f, 260f, 40f), shoot.CurrentMag + " / " + shoot.CurrentReserve, style);

        // Полоска перезарядки
        if (shoot.IsReloading)
        {
            float w = 220f;
            GUI.Box(new Rect(cx - w * 0.5f, cy + 60f, w, 22f), "");
            GUI.Box(new Rect(cx - w * 0.5f + 2f, cy + 62f, (w - 4f) * shoot.ReloadProgress, 18f), "");
        }
    }

    static void DrawRect(float x, float y, float w, float h)
    {
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
    }
}
