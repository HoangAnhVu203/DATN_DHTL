using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageCaster : MonoBehaviour
{
    private const float DuplicateHitLockSeconds = 0.25f;
    private static readonly Dictionary<long, float> RecentNetworkHitTimes = new();

    private Collider damageCasterCollider;
    private PlayerVFXManager ownerVFXManager;
    private Character ownerCharacter;
    private FusionPlayerAvatar ownerNetworkAvatar;
    private FusionEnemyAvatar ownerNetworkEnemy;
    private Transform ownerRoot;

    public int damage = 30;
    public string targetTag;
    private List<Character> damageTargetList;
    private HashSet<int> damageTargetIdSet;
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
        damageTargetIdSet = new HashSet<int>();
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

        if (targetCharacter != null && HasDamagedTarget(targetCharacter))
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
            if (HasDamagedTarget(targetNetworkEnemy) || IsRecentNetworkHit(targetNetworkEnemy))
            {
                return;
            }

            MarkRecentNetworkHit(targetNetworkEnemy);
            PlayerRef attacker = ownerNetworkAvatar != null ? ownerNetworkAvatar.NetworkPlayerRef : PlayerRef.None;
            if (targetNetworkEnemy.RequestDamage(damage, attackerPosition, GetDamageSourceId(), attacker))
            {
                PlayHitVFX(other);
                MarkDamagedTarget(targetNetworkEnemy);
                MarkDamagedTarget(targetCharacter);
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
        MarkDamagedTarget(targetCharacter);
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
            if (HasDamagedTarget(targetNetworkPlayer) || IsRecentNetworkHit(targetNetworkPlayer))
            {
                return;
            }

            MarkRecentNetworkHit(targetNetworkPlayer);
            if (targetNetworkPlayer.RequestDamage(damage, attackerPosition, GetDamageSourceId()))
            {
                PlayHitVFX(other);
                MarkDamagedTarget(targetNetworkPlayer);
                MarkDamagedTarget(targetCharacter);
            }

            return;
        }

        if (targetCharacter is not Player || HasDamagedTarget(targetCharacter))
        {
            return;
        }

        targetCharacter.ApplyDamage(damage, attackerPosition);
        PlayHitVFX(other);
        MarkDamagedTarget(targetCharacter);
    }

    private bool HasDamagedTarget(Component target)
    {
        return target != null && damageTargetIdSet.Contains(target.GetInstanceID());
    }

    private void MarkDamagedTarget(Component target)
    {
        if (target == null)
        {
            return;
        }

        damageTargetIdSet.Add(target.GetInstanceID());

        if (target is Character character && !damageTargetList.Contains(character))
        {
        damageTargetList.Add(character);
        }
    }

    private bool IsRecentNetworkHit(Component target)
    {
        long key = GetOwnerTargetKey(target);
        if (key == 0)
        {
            return false;
        }

        float now = Time.time;
        if (!RecentNetworkHitTimes.TryGetValue(key, out float lastHitTime))
        {
            return false;
        }

        return now - lastHitTime < DuplicateHitLockSeconds;
    }

    private void MarkRecentNetworkHit(Component target)
    {
        long key = GetOwnerTargetKey(target);
        if (key == 0)
        {
            return;
        }

        RecentNetworkHitTimes[key] = Time.time;
    }

    private long GetOwnerTargetKey(Component target)
    {
        if (target == null)
        {
            return 0;
        }

        int ownerId = ownerRoot != null ? ownerRoot.GetInstanceID() : transform.root.GetInstanceID();
        int targetId = target.transform.root.GetInstanceID();
        return ((long)ownerId << 32) ^ (uint)targetId;
    }

    private int GetDamageSourceId()
    {
        return ownerRoot != null ? ownerRoot.GetInstanceID() : transform.root.GetInstanceID();
    }

    private void ClearDamagedTargets()
    {
        damageTargetList.Clear();
        damageTargetIdSet.Clear();
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
        if (ownerNetworkAvatar != null && ownerNetworkAvatar.Object != null && ownerNetworkAvatar.Object.IsValid)
        {
            return;
        }

        if (controlledDamageWindowActive)
        {
            damageCasterCollider.enabled = true;
            return;
        }

        if (damageCasterCollider.enabled)
        {
            return;
        }

        ClearDamagedTargets();
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
            ClearDamagedTargets();
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
        ClearDamagedTargets();
        damageCasterCollider.enabled = false;
    }
}
