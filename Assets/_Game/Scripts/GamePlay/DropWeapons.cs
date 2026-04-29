using System.Collections.Generic;
using UnityEngine;

public class DropWeapons : MonoBehaviour
{
    public List<GameObject> weapons;

    public void DropSword()
    {
        foreach (GameObject weapon in weapons)
        {
            weapon.AddComponent<Rigidbody>();
            weapon.AddComponent<BoxCollider>();
            weapon.transform.parent = null;
        }
    }
}
