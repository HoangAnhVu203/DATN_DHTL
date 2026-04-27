using UnityEngine;
using UnityEngine.VFX;

public class EnemyVFXManager : MonoBehaviour
{
    public VisualEffect footStep;

    public void BurstFootStep()
    {
        footStep.SendEvent("OnPlay");
    }
}
