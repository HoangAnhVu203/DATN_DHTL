using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageCaster : MonoBehaviour
{
    private Collider damageCasterCollider;
    private PlayerVFXManager ownerVFXManager;
    private Character ownerCharacter;
    private Transform ownerRoot;

    public int damage = 30;
    public string targetTag;
    private List<Character> damageTargetList;

    private void Awake()
    {
        damageCasterCollider = GetComponent<Collider>();
        damageCasterCollider.isTrigger = true;
        damageCasterCollider.enabled = false;
        ownerVFXManager = GetComponentInParent<PlayerVFXManager>();
        ownerCharacter = GetComponentInParent<Character>();
        ownerRoot = ownerCharacter != null ? ownerCharacter.transform : null;
        damageTargetList = new List<Character>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        DamageOrb damageOrb = other.GetComponentInParent<DamageOrb>();
        if (damageOrb != null && ownerCharacter is Player)
        {
            damageOrb.DestroyOrb();
            PlayHitVFX(other);
            return;
        }

        Character targetCharacter = other.GetComponentInParent<Character>();
        if (targetCharacter == null
            || !targetCharacter.CompareTag(targetTag)
            || damageTargetList.Contains(targetCharacter))
        {
            return;
        }

        Vector3 attackerPosition = ownerRoot != null ? ownerRoot.position : transform.position;
        targetCharacter.ApplyDamage(damage, attackerPosition);
        PlayHitVFX(other);
        damageTargetList.Add(targetCharacter);
    }

    private void PlayHitVFX(Collider targetCollider)
    {
        if (ownerVFXManager == null)
        {
            return;
        }

        Vector3 hitPosition = targetCollider.ClosestPoint(transform.position);
        ownerVFXManager.PlaySlash(hitPosition);
    }

    public void EnableDamageCaster()
    {
        damageTargetList.Clear();
        damageCasterCollider.enabled = true;
    }

    public void DisableDamageCaster()
    {
        damageTargetList.Clear();
        damageCasterCollider.enabled = false;
    }
}
