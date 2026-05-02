using UnityEngine;

public class PickUp : MonoBehaviour
{
    public PickUpType type;
    public int value = 20;

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            other.gameObject.GetComponent<Character>().PickUpItem(this);
            Destroy(gameObject);
        }
    }
}
