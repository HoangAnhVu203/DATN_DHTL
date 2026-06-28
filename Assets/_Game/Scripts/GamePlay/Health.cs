using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public bool IsDead => currentHealth <= 0;
    public event Action<int, int> HealthChanged;
    private int baseMaxHealth;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        baseMaxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    // Updates the health from network.
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

    // Applies the max health multiplier.
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

    // Applies the damage.
    public void ApplyDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - damage, 0);
        NotifyHealthChanged();
    }

    // Adds the health.
    public void AddHealth(int health)
    {
        currentHealth = Mathf.Clamp(currentHealth + health, 0, maxHealth);

        NotifyHealthChanged();
    }

    // Notifies listeners that health changed occurred.
    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
