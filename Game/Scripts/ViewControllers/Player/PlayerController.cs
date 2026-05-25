using FFramework.Core;
using UnityEngine;

public class PlayerController : ArchitectureViewController
{
    public PlayAnima playAnima;
    public AnimationClip idle;
    public AnimationClip run;
    public AnimationClip dead;

    private void Start()
    {
        playAnima.PlayAnimaClip(idle);
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playAnima.PlayAnimaClip(run);
        }
    }
}
