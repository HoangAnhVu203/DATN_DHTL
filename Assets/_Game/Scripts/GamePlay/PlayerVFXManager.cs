using UnityEngine;
using UnityEngine.VFX;

public class PlayerVFXManager : MonoBehaviour
{
    private const string PlayEventName = "OnPlay";
    private const string StopEventName = "OnStop";

    public VisualEffect footStep;
    public ParticleSystem Blade_01;
    public VisualEffect slash;
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
        if (Blade_01 != null)
        {
            Blade_01.Play();
        }
    }

    public void PlaySlash(Vector3 pos)
    {
        if (slash == null)
        {
            return;
        }

        slash.transform.position = pos;
        slash.Play();
    }
}
