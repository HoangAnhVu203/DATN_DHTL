using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public bool IsDead => currentHealth <= 0;
    public event Action<int, int> HealthChanged;
    private int baseMaxHealth;

    private void Awake()
    {
        baseMaxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    public void SetHealthFromNetwork(int current, int max)
    {
        int previousMax = maxHealth;
        maxHealth = Mathf.Max(1, max);
        int clampedCurrent = Mathf.Clamp(current, 0, maxHealth);

        if (currentHealth == clampedCurrent && previousMax == maxHealth)
        {
            return;
        }

        currentHealth = clampedCurrent;
        NotifyHealthChanged();
    }

    public void ApplyMaxHealthMultiplier(float multiplier, bool refillHealth)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        if (baseMaxHealth <= 0)
        {
            baseMaxHealth = Mathf.Max(1, maxHealth);
        }

        int scaledMaxHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * safeMultiplier));
        int scaledCurrentHealth = refillHealth
            ? scaledMaxHealth
            : Mathf.Clamp(currentHealth, 0, scaledMaxHealth);

        SetHealthFromNetwork(scaledCurrentHealth, scaledMaxHealth);
    }

    public void ApplyDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - damage, 0);
        NotifyHealthChanged();
    }

    public void AddHealth(int health)
    {
        currentHealth = Mathf.Clamp(currentHealth + health, 0, maxHealth);

        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
