using UnityEngine;
using UnityEngine.VFX;

public class PlayerVFXManager : MonoBehaviour
{
    private const string PlayEventName = "OnPlay";
    private const string StopEventName = "OnStop";

    public VisualEffect footStep;
    public ParticleSystem Blade_01;
    private bool isFootStepPlaying;

    public void Update_FootStep(bool state)
    {
        if (footStep == null || isFootStepPlaying == state)
        {
            return;
        }

        isFootStepPlaying = state;

        if (state)
        {
            footStep.SendEvent(PlayEventName);
        }
        else
        {
            footStep.SendEvent(StopEventName);
            footStep.Stop();
        }
    }

    private void OnDisable()
    {
        Update_FootStep(false);
    }

    public void PlayBlade01()
    {
        Blade_01.Play();
    }
}
