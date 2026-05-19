using UnityEngine;

public class DamageOrb : MonoBehaviour
{
    public float speed = 2f;
    public int damage = 10;
    public ParticleSystem hitVFX;
    private Rigidbody rb;
    private FusionDamageOrb fusionDamageOrb;
    private bool isDestroyed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fusionDamageOrb = GetComponent<FusionDamageOrb>();
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
