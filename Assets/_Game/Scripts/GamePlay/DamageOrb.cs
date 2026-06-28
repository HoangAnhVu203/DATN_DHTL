using UnityEngine;

public class DamageOrb : MonoBehaviour
{
    public float speed = 2f;
    public int damage = 10;
    public ParticleSystem hitVFX;
    private Rigidbody rb;
    private FusionDamageOrb fusionDamageOrb;
    private bool isDestroyed;
    private int baseDamage;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        baseDamage = Mathf.Max(0, damage);
        rb = GetComponent<Rigidbody>();
        fusionDamageOrb = GetComponent<FusionDamageOrb>();
    }

    // Applies the damage multiplier.
    public void ApplyDamageMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        if (baseDamage <= 0)
        {
            baseDamage = Mathf.Max(0, damage);
        }

        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * safeMultiplier));
    }

    // Updates the damage from network.
    public void SetDamageFromNetwork(int networkDamage)
    {
        damage = Mathf.Max(1, networkDamage);
    }

    // Runs the physics-timed update for this behaviour.
    private void FixedUpdate()
    {
        if (fusionDamageOrb != null && !fusionDamageOrb.CanSimulateLocally)
        {
            return;
        }

        rb.MovePosition(transform.position +  transform.forward * speed * Time.deltaTime);
    }

    // Handles the first contact with another collider.
    private void OnTriggerEnter(Collider other)
    {
        if (fusionDamageOrb != null && !fusionDamageOrb.CanSimulateLocally)
        {
            return;
        }

        FusionPlayerAvatar targetNetworkPlayer = other.GetComponentInParent<FusionPlayerAvatar>();
        if (targetNetworkPlayer != null)
        {
            targetNetworkPlayer.RequestDamage(damage, transform.position);
            DestroyOrb();
            return;
        }

        Character targetCharacter = other.GetComponentInParent<Character>();
        if (targetCharacter is Player)
        {
            targetCharacter.ApplyDamage(damage, transform.position);
            DestroyOrb();
            return;
        }

        if (targetCharacter != null)
        {
            return;
        }

        DestroyOrb();
    }

    // Destroys the orb.
    public void DestroyOrb()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;

        if (fusionDamageOrb != null && fusionDamageOrb.IsNetworkSpawned)
        {
            fusionDamageOrb.DestroyNetworkOrb(transform.position);
            return;
        }

        PlayHitVFX(transform.position);
        Destroy(gameObject);
    }

    // Plays the hit vfx.
    public void PlayHitVFX(Vector3 hitPosition)
    {
        if (hitVFX != null)
        {
            Instantiate(hitVFX, hitPosition, Quaternion.identity);
        }
    }
}
