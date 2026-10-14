using UnityEngine;

/// <summary>
/// Вспышка выстрела: квад со звездой + точечный свет на дуле.
/// Живёт постоянно на вьюмодели, показывается на короткое время через Show().
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    public Renderer quadRenderer;
    public Light flashLight;

    public float minScale = 0.12f;
    public float maxScale = 0.2f;
    public float duration = 0.05f;

    private float timer;

    void Awake()
    {
        if (quadRenderer == null) quadRenderer = GetComponent<Renderer>();
        if (flashLight == null) flashLight = GetComponentInChildren<Light>();
        Hide();
    }

    public void Show()
    {
        timer = duration;

        float s = Random.Range(minScale, maxScale);
        transform.localScale = new Vector3(s, s, s);
        transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        if (quadRenderer != null) quadRenderer.enabled = true;
        if (flashLight != null)
        {
            flashLight.enabled = true;
            flashLight.intensity = Random.Range(2f, 3.5f);
        }
    }

    void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f) Hide();
    }

    void Hide()
    {
        timer = 0f;
        if (quadRenderer != null) quadRenderer.enabled = false;
        if (flashLight != null) flashLight.enabled = false;
    }
}
