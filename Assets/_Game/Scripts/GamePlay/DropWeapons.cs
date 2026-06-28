using System.Collections.Generic;
using UnityEngine;

public class DropWeapons : MonoBehaviour
{
    public List<GameObject> weapons;
    private readonly List<WeaponInitialState> initialStates = new List<WeaponInitialState>();

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        CacheInitialStates();
    }

    // Drops the sword.
    public void DropSword()
    {
        CacheInitialStates();

        foreach (GameObject weapon in weapons)
        {
            if (weapon == null)
            {
                continue;
            }

            if (weapon.GetComponent<Rigidbody>() == null)
            {
                weapon.AddComponent<Rigidbody>();
            }

            if (weapon.GetComponent<Collider>() == null)
            {
                weapon.AddComponent<BoxCollider>();
            }

            weapon.transform.parent = null;
        }
    }

    // Picks up the weapons.
    public void PickUpWeapons()
    {
        CacheInitialStates();

        foreach (WeaponInitialState initialState in initialStates)
        {
            if (initialState.weapon == null || initialState.parent == null)
            {
                continue;
            }

            Rigidbody weaponRigidbody = initialState.weapon.GetComponent<Rigidbody>();
            if (weaponRigidbody != null && weaponRigidbody != initialState.originalRigidbody)
            {
                Destroy(weaponRigidbody);
            }

            Collider[] weaponColliders = initialState.weapon.GetComponents<Collider>();
            foreach (Collider weaponCollider in weaponColliders)
            {
                if (initialState.originalColliders.Contains(weaponCollider))
                {
                    continue;
                }

                Destroy(weaponCollider);
            }

            Transform weaponTransform = initialState.weapon.transform;
            weaponTransform.SetParent(initialState.parent);
            weaponTransform.localPosition = initialState.localPosition;
            weaponTransform.localRotation = initialState.localRotation;
            weaponTransform.localScale = initialState.localScale;
            initialState.weapon.SetActive(true);
        }
    }

    private void CacheInitialStates()
    {
        if (initialStates.Count > 0 || weapons == null)
        {
            return;
        }

        foreach (GameObject weapon in weapons)
        {
            if (weapon == null)
            {
                continue;
            }

            Transform weaponTransform = weapon.transform;
            initialStates.Add(new WeaponInitialState
            {
                weapon = weapon,
                parent = weaponTransform.parent,
                localPosition = weaponTransform.localPosition,
                localRotation = weaponTransform.localRotation,
                localScale = weaponTransform.localScale,
                originalRigidbody = weapon.GetComponent<Rigidbody>(),
                originalColliders = new List<Collider>(weapon.GetComponents<Collider>())
            });
        }
    }

    private class WeaponInitialState
    {
        public GameObject weapon;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Rigidbody originalRigidbody;
        public List<Collider> originalColliders;
    }
}
