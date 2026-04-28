using UnityEngine;
using UnityEngine.VFX;

public class EnemyVFXManager : MonoBehaviour
{
    private const string BeingHitVFXName = "Particle BeingHit";

    public VisualEffect footStep;
    public VisualEffect attackVFX;
    public ParticleSystem beingHitVFX;

    private void Awake()
    {
        CacheBeingHitVFX();
    }

    public void BurstFootStep()
    {
        if (footStep != null)
        {
            footStep.SendEvent("OnPlay");
        }
    }

    public void PlayAttackVFX()
    {
        if (attackVFX != null)
        {
            attackVFX.SendEvent("OnPlay");
        }
    }

    public void PlayBeingHitVFX(Vector3 attackerPos)
    {
        CacheBeingHitVFX();

        if (beingHitVFX == null)
        {
            return;
        }

        Vector3 forceForward = transform.position - attackerPos;
        forceForward.y = 0;
        if (forceForward.sqrMagnitude <= 0.001f)
        {
            forceForward = -transform.forward;
        }
        else
        {
            forceForward.Normalize();
        }

        beingHitVFX.transform.position = transform.position + Vector3.up;
        beingHitVFX.transform.rotation = Quaternion.LookRotation(forceForward);
        beingHitVFX.Play();
    }

    private void CacheBeingHitVFX()
    {
        if (beingHitVFX != null)
        {
            return;
        }

        ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem.name == BeingHitVFXName)
            {
                beingHitVFX = particleSystem;
                return;
            }
        }
    }
}
