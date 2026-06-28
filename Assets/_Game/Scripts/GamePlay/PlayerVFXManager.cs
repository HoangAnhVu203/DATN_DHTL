using UnityEngine;
using UnityEngine.VFX;

public class PlayerVFXManager : MonoBehaviour
{
    private const string PlayEventName = "OnPlay";
    private const string StopEventName = "OnStop";

    public VisualEffect footStep;
    public ParticleSystem Blade_01;
    public ParticleSystem Blade_02;
    public ParticleSystem Blade_03;

    public VisualEffect slash;
    public VisualEffect heal;
    private bool isFootStepPlaying;


    // Updates the foot step.
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

    // Clears temporary state when this component is disabled.
    private void OnDisable()
    {
        Update_FootStep(false);
    }

    // Plays the blade01.
    public void PlayBlade01()
    {
        if (Blade_01 != null)
        {
            Blade_01.Play();
        }
    }
    // Plays the blade02.
    public void PlayBlade02()
    {
        if (Blade_02 != null)
        {
            Blade_02.Play();
        }
    }
    // Plays the blade03.
    public void PlayBlade03()
    {
        if (Blade_03 != null)
        {
            Blade_03.Play();
        }
    }

    // Stops the blade process.
    public void StopBlade()
    {
        Blade_01.Simulate(0);
        Blade_01.Stop();

        Blade_02.Simulate(0);
        Blade_02.Stop();

        Blade_03.Simulate(0);
        Blade_03.Stop();
    }

    // Plays the slash.
    public void PlaySlash(Vector3 pos)
    {
        if (slash == null)
        {
            return;
        }

        slash.transform.position = pos;
        slash.Play();
    }

    // Plays the er health vfx.
    public void PlayerHealthVFX()
    {
        if (heal != null)
        {
            heal.Play();
        }
    }
}
