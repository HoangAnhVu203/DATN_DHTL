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
    private int baseDamage;

    // Sets up this component before gameplay starts.
    private void Awake()
    {
        baseDamage = Mathf.Max(0, damage);
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

    // Applies the damage multiplier.
    public void ApplyDamageMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        if (baseDamage <= 0)
        {
            baseDamage = Mathf.Max(0, damage);
        }

        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * safeMultiplier));
    }

    // Handles the first contact with another collider.
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    // Processes a collider while it stays inside the trigger.
    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    // Tries to apply damage.
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

    // Tries to apply enemy damage.
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

    // Checks whether damaged target is available.
    private bool HasDamagedTarget(Component target)
    {
        return target != null && damageTargetIdSet.Contains(target.GetInstanceID());
    }

    // Records a target already hit during this damage window.
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

    // Checks whether this target was hit recently over the network.
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

    // Records a recent network hit to avoid duplicate damage.
    private void MarkRecentNetworkHit(Component target)
    {
        long key = GetOwnerTargetKey(target);
        if (key == 0)
        {
            return;
        }

        RecentNetworkHitTimes[key] = Time.time;
    }

    // Returns the owner target key.
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

    // Returns the damage source id.
    private int GetDamageSourceId()
    {
        return ownerRoot != null ? ownerRoot.GetInstanceID() : transform.root.GetInstanceID();
    }

    // Clears the damaged targets.
    private void ClearDamagedTargets()
    {
        damageTargetList.Clear();
        damageTargetIdSet.Clear();
    }

    // Plays the hit vfx.
    private void PlayHitVFX(Collider targetCollider)
    {
        if (ownerVFXManager == null)
        {
            return;
        }

        Vector3 hitPosition = targetCollider.ClosestPoint(transform.position);
        ownerVFXManager.PlaySlash(hitPosition);
    }

    // Enables the damage caster.
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

    // Disables the damage caster.
    public void DisableDamageCaster()
    {
        if (controlledDamageWindowActive)
        {
            return;
        }

        ForceDisableDamageCaster();
    }

    // Starts a damage window controlled by animation or network code.
    public void BeginControlledDamageWindow()
    {
        if (!controlledDamageWindowActive)
        {
            controlledDamageWindowActive = true;
            ClearDamagedTargets();
        }

        damageCasterCollider.enabled = true;
    }

    // Ends the controlled damage window process.
    public void EndControlledDamageWindow()
    {
        controlledDamageWindowActive = false;
        ForceDisableDamageCaster();
    }

    // Forces the disable damage caster.
    public void ForceDisableDamageCaster()
    {
        controlledDamageWindowActive = false;
        ClearDamagedTargets();
        damageCasterCollider.enabled = false;
    }
}
