using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Здоровье. Пока используется мишенями (respawnOnDeath) и игроком
/// (для сетевой стрельбы в фазе 2, урон получает через Hitbox/TakeDamage).
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;

    [Header("Респавн (для мишеней)")]
    [Tooltip("Вкл: после смерти объект падает и встаёт обратно через respawnDelay (мишени)")]
    public bool respawnOnDeath = false;
    public float respawnDelay = 3f;

    public float Current { get; private set; }
    public bool IsAlive => Current > 0f;

    /// Урон получен (для HUD/индикатора в фазе 4-5)
    public event Action Damaged;
    /// Смерть (для раундов в фазе 3)
    public event Action Died;
    /// Изменение здоровья (текущее, максимальное)
    public event Action<float, float> OnHealthChanged;

    private Collider[] hitboxColliders;


    void Awake()
    {
        Current = maxHealth;
        CacheHitboxes();
    }

    void CacheHitboxes()
    {
        var hitboxes = GetComponentsInChildren<Hitbox>(true);
        hitboxColliders = new Collider[hitboxes.Length];
        for (int i = 0; i < hitboxes.Length; i++)
            hitboxColliders[i] = hitboxes[i].GetComponent<Collider>();
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (!IsAlive) return;

        Current = Mathf.Max(0f, Current - amount);
        OnHealthChanged?.Invoke(Current, maxHealth);
        Damaged?.Invoke();
        if (Current <= 0f) Die();
    }


    void Die()
    {
        Died?.Invoke();
        SetHitboxesEnabled(false);

        if (respawnOnDeath) StartCoroutine(RespawnRoutine());
        // Для настоящего игрока смертью займётся раундовая логика (фаза 3)
    }

    void SetHitboxesEnabled(bool on)
    {
        if (hitboxColliders == null) return;
        foreach (var col in hitboxColliders)
            if (col != null) col.enabled = on;
    }

    IEnumerator RespawnRoutine()
    {
        // Падение набок
        Quaternion upright = transform.rotation;
        Quaternion fallen = upright * Quaternion.Euler(0f, 0f, 85f);

        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(upright, fallen, t / 0.35f);
            yield return null;
        }

        yield return new WaitForSeconds(respawnDelay);

        transform.rotation = upright;
        Current = maxHealth;
        OnHealthChanged?.Invoke(Current, maxHealth);
        SetHitboxesEnabled(true);
    }

}
