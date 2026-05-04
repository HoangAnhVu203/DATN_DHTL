using UnityEngine;

public class DamageOrb : MonoBehaviour
{
    public float speed = 2f;
    public int damage = 10;
    public ParticleSystem hitVFX;
    private Rigidbody rb;
    private bool isDestroyed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        rb.MovePosition(transform.position +  transform.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        Character targetCharacter = other.GetComponentInParent<Character>();

        if (targetCharacter is Player)
        {
            targetCharacter.ApplyDamage(damage, transform.position);
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

        if (hitVFX != null)
        {
            Instantiate(hitVFX, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
