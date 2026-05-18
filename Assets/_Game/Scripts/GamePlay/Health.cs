using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public bool IsDead => currentHealth <= 0;
    public event Action<int, int> HealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    public void SetHealthFromNetwork(int current, int max)
    {
        maxHealth = Mathf.Max(1, max);
        int clampedCurrent = Mathf.Clamp(current, 0, maxHealth);

        if (currentHealth == clampedCurrent)
        {
            return;
        }

        currentHealth = clampedCurrent;
        NotifyHealthChanged();
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
