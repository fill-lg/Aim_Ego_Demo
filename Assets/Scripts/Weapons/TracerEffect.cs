using UnityEngine;

/// <summary>
/// Трассер выстрела: линия от дула до точки попадания, гаснет за tracerTime.
/// </summary>
public class TracerEffect : MonoBehaviour
{
    private LineRenderer line;
    private float life;
    private float t;
    private Color color;
    private float baseAlpha;

    public static TracerEffect Spawn(Vector3 from, Vector3 to, Material material, float width, float lifetime, Color tint)
    {
        var go = new GameObject("Tracer");
        var self = go.AddComponent<TracerEffect>();
        self.life = Mathf.Max(0.01f, lifetime);
        self.color = tint;
        self.baseAlpha = tint.a;

        self.line = go.AddComponent<LineRenderer>();
        self.line.positionCount = 2;
        self.line.SetPosition(0, from);
        self.line.SetPosition(1, to);
        self.line.widthMultiplier = width;
        self.line.material = material;
        self.line.useWorldSpace = true;
        self.line.alignment = LineAlignment.View;
        self.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Destroy(go, lifetime);
        return self;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(1f - t / life);
        line.material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, baseAlpha * k));
    }
}
