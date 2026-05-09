using UnityEngine;

public class PickUp : MonoBehaviour
{
    public PickUpType type;
    public int value = 20;

    public ParticleSystem collectedVFX;

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            other.gameObject.GetComponent<Character>().PickUpItem(this);

            if(collectedVFX != null)
            {
                Instantiate(collectedVFX, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}
