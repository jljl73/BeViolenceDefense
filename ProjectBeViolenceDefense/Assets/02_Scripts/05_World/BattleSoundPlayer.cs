using UnityEngine;

// Scene-specific combat clips; playback and settings belong to SoundManager.
[DisallowMultipleComponent]
public sealed class BattleSoundPlayer : MonoBehaviour
{
    private const int MaxHitVoices = 6;
    private const float HitVolume = 0.24f;

    private AudioClip hitClip;

    public void Initialize(AudioClip clip)
    {
        hitClip = clip;
    }

    public void PlayHit()
    {
        if (hitClip == null)
            return;

        SoundManager.Instance?.PlaySfx(hitClip, HitVolume, MaxHitVoices);
    }
}
