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

    private void Awake()
    {
        baseDamage = Mathf.Max(0, damage);
        rb = GetComponent<Rigidbody>();
        fusionDamageOrb = GetComponent<FusionDamageOrb>();
    }

    public void ApplyDamageMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        if (baseDamage <= 0)
        {
            baseDamage = Mathf.Max(0, damage);
        }

        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * safeMultiplier));
    }

    public void SetDamageFromNetwork(int networkDamage)
    {
        damage = Mathf.Max(1, networkDamage);
    }

    private void FixedUpdate()
    {
        if (fusionDamageOrb != null && !fusionDamageOrb.CanSimulateLocally)
        {
            return;
        }

        rb.MovePosition(transform.position +  transform.forward * speed * Time.deltaTime);
    }

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

    public void PlayHitVFX(Vector3 hitPosition)
    {
        if (hitVFX != null)
        {
            Instantiate(hitVFX, hitPosition, Quaternion.identity);
        }
    }
}
