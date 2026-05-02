using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ApplyDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - damage, 0);
    }

    public void AddHealth(int health)
    {
        currentHealth += health;

        if(currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }
}
