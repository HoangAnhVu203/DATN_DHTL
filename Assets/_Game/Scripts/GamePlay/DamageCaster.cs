using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageCaster : MonoBehaviour
{
    private Collider damageCasterCollider;
    private PlayerVFXManager ownerVFXManager;
    private Character ownerCharacter;
    private FusionPlayerAvatar ownerNetworkAvatar;
    private FusionEnemyAvatar ownerNetworkEnemy;
    private Transform ownerRoot;

    public int damage = 30;
    public string targetTag;
    private List<Character> damageTargetList;
    private bool controlledDamageWindowActive;

    private void Awake()
    {
        damageCasterCollider = GetComponent<Collider>();
        damageCasterCollider.isTrigger = true;
        damageCasterCollider.enabled = false;
        ownerVFXManager = GetComponentInParent<PlayerVFXManager>();
        ownerCharacter = GetComponentInParent<Character>();
        ownerNetworkAvatar = GetComponentInParent<FusionPlayerAvatar>();
        ownerNetworkEnemy = GetComponentInParent<FusionEnemyAvatar>();
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
        if (ownerNetworkAvatar != null && !ownerNetworkAvatar.CanApplyDamageLocally)
        {
            return;
        }

        DamageOrb damageOrb = other.GetComponentInParent<DamageOrb>();
        if (damageOrb != null && ownerCharacter is Player)
        {
            damageOrb.DestroyOrb();
            PlayHitVFX(other);
            return;
        }

        Character targetCharacter = other.GetComponentInParent<Character>();
        if (targetCharacter != null && targetCharacter == ownerCharacter)
        {
            return;
        }

        if (targetCharacter != null && damageTargetList.Contains(targetCharacter))
        {
            return;
        }

        Vector3 attackerPosition = ownerRoot != null ? ownerRoot.position : transform.position;

        if (ownerNetworkEnemy != null || ownerCharacter is Enemy)
        {
            TryApplyEnemyDamage(other, targetCharacter, attackerPosition);
            return;
        }

        FusionEnemyAvatar targetNetworkEnemy = other.GetComponentInParent<FusionEnemyAvatar>();
        if (targetNetworkEnemy != null)
        {
            if (targetNetworkEnemy.RequestDamage(damage, attackerPosition))
            {
                PlayHitVFX(other);

                if (targetCharacter != null)
                {
                    damageTargetList.Add(targetCharacter);
                }
            }

            return;
        }

        if (targetCharacter == null
            || !targetCharacter.CompareTag(targetTag))
        {
            return;
        }

        targetCharacter.ApplyDamage(damage, attackerPosition);
        PlayHitVFX(other);
        damageTargetList.Add(targetCharacter);
    }

    private void TryApplyEnemyDamage(Collider other, Character targetCharacter, Vector3 attackerPosition)
    {
        FusionEnemyAvatar targetNetworkEnemy = other.GetComponentInParent<FusionEnemyAvatar>();
        if (targetNetworkEnemy != null)
        {
            return;
        }

        FusionPlayerAvatar targetNetworkPlayer = other.GetComponentInParent<FusionPlayerAvatar>();
        if (targetNetworkPlayer != null)
        {
            if (targetNetworkPlayer.RequestDamage(damage, attackerPosition))
            {
                PlayHitVFX(other);

                if (targetCharacter != null)
                {
                    damageTargetList.Add(targetCharacter);
                }
            }

            return;
        }

        if (targetCharacter is not Player)
        {
            return;
        }

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
        if (damageCasterCollider.enabled)
        {
            return;
        }

        damageTargetList.Clear();
        damageCasterCollider.enabled = true;
    }

    public void DisableDamageCaster()
    {
        if (controlledDamageWindowActive)
        {
            return;
        }

        ForceDisableDamageCaster();
    }

    public void BeginControlledDamageWindow()
    {
        if (!controlledDamageWindowActive)
        {
            controlledDamageWindowActive = true;
            damageTargetList.Clear();
        }

        damageCasterCollider.enabled = true;
    }

    public void EndControlledDamageWindow()
    {
        controlledDamageWindowActive = false;
        ForceDisableDamageCaster();
    }

    public void ForceDisableDamageCaster()
    {
        controlledDamageWindowActive = false;
        damageTargetList.Clear();
        damageCasterCollider.enabled = false;
    }
}
