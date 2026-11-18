using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoundChannel { Bgm, Sfx, Ui }

// Presentation-only audio. Simulation code must not depend on this manager.
[DisallowMultipleComponent]
public sealed class SoundManager : MonoBehaviour
{
    public const int MaxSfxVoices = 12;
    public const int MaxUiVoices = 4;
    private const string SettingsPrefix = "BeViolenceDefense.Audio.";

    private sealed class Voice
    {
        public AudioSource Source;
        public float Gain;
    }

    private static SoundManager instance;
    private static bool quitting;
    private Voice music;
    private Voice[] effects;
    private Voice[] ui;
    private bool initialized;
    private bool settingsDirty;

    public static SoundManager Instance
    {
        get
        {
            if (quitting || !Application.isPlaying) return null;
            if (instance != null) return instance;
            var existing = FindFirstObjectByType<SoundManager>();
            if (existing != null) existing.Initialize();
            else new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();
            return instance;
        }
    }

    public float MasterVolume { get; private set; } = 1f;
    public float BgmVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;
    public float UiVolume { get; private set; } = 1f;
    public bool Muted { get; private set; }
    public AudioClip CurrentBgm => music?.Source.clip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => _ = Instance;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (!Application.isPlaying) return;
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (initialized) return;
        initialized = true;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        MasterVolume = ReadVolume("Master");
        BgmVolume = ReadVolume("Bgm");
        SfxVolume = ReadVolume("Sfx");
        UiVolume = ReadVolume("Ui");
        Muted = PlayerPrefs.GetInt(SettingsPrefix + "Muted", 0) != 0;
        music = CreateVoice("BGM", 32);
        effects = CreatePool("SFX", MaxSfxVoices, 128);
        ui = CreatePool("UI", MaxUiVoices, 64);
        foreach (Voice voice in ui) voice.Source.ignoreListenerPause = true;
        ApplyVolumes();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static float ReadVolume(string key) => NormalizeVolume(PlayerPrefs.GetFloat(SettingsPrefix + key, 1f));
    private static float NormalizeVolume(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp01(value);

    private Voice CreateVoice(string label, int priority)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = priority;
        return new Voice { Source = source, Gain = 1f };
    }

    private Voice[] CreatePool(string label, int count, int priority)
    {
        var pool = new Voice[count];
        for (int i = 0; i < count; i++) pool[i] = CreateVoice($"{label} {i + 1:00}", priority);
        return pool;
    }

    // Calling again with the same playing clip changes gain without restarting it.
    // A missing clip leaves the current music alone; use StopBgm to stop explicitly.
    public bool PlayBgm(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (!initialized || clip == null) return false;
        music.Gain = NormalizeVolume(volume);
        music.Source.loop = loop;
        ApplyVolume(music, BgmVolume);
        if (music.Source.clip == clip && music.Source.isPlaying) return true;
        music.Source.Stop();
        music.Source.clip = clip;
        music.Source.Play();
        return true;
    }

    public void StopBgm()
    {
        if (!initialized) return;
        music.Source.Stop();
        music.Source.clip = null;
    }

    // Return false when muted, missing a clip, or the pool/clip limit is full.
    // Never interrupt an existing effect to accommodate a crowd of new hits.
    public bool PlaySfx(AudioClip clip, float volume = 1f, int maxInstances = 0) =>
        Play(effects, SfxVolume, clip, volume, maxInstances);

    public bool PlayUi(AudioClip clip, float volume = 1f, int maxInstances = 0) =>
        Play(ui, UiVolume, clip, volume, maxInstances);

    private bool Play(Voice[] pool, float channelVolume, AudioClip clip, float gain, int maxInstances)
    {
        gain = NormalizeVolume(gain);
        if (!initialized || clip == null || Muted || MasterVolume * channelVolume * gain <= 0f) return false;
        Voice free = null;
        int instances = 0;
        foreach (Voice voice in pool)
        {
            if (voice.Source.isPlaying)
            {
                if (voice.Source.clip == clip) instances++;
            }
            else if (free == null) free = voice;
        }
        if (free == null || (maxInstances > 0 && instances >= maxInstances)) return false;
        free.Gain = gain;
        free.Source.clip = clip;
        free.Source.loop = false;
        ApplyVolume(free, channelVolume);
        free.Source.Play();
        return true;
    }

    public void StopSfx()
    {
        if (initialized) Stop(effects);
    }

    public void StopUi()
    {
        if (initialized) Stop(ui);
    }

    public void StopAll()
    {
        StopBgm(); StopSfx(); StopUi();
    }

    private static void Stop(Voice[] pool)
    {
        foreach (Voice voice in pool)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
        }
    }

    public float GetVolume(SoundChannel channel) => channel switch
    {
        SoundChannel.Bgm => BgmVolume,
        SoundChannel.Sfx => SfxVolume,
        SoundChannel.Ui => UiVolume,
        _ => throw new System.ArgumentOutOfRangeException(nameof(channel))
    };

    public void SetMasterVolume(float value)
    {
        MasterVolume = NormalizeVolume(value);
        PlayerPrefs.SetFloat(SettingsPrefix + "Master", MasterVolume);
        settingsDirty = true;
        ApplyVolumes();
    }

    public void SetVolume(SoundChannel channel, float value)
    {
        value = NormalizeVolume(value);
        switch (channel)
        {
            case SoundChannel.Bgm: BgmVolume = value; break;
            case SoundChannel.Sfx: SfxVolume = value; break;
            case SoundChannel.Ui: UiVolume = value; break;
            default: throw new System.ArgumentOutOfRangeException(nameof(channel));
        }
        PlayerPrefs.SetFloat(SettingsPrefix + channel, value);
        settingsDirty = true;
        ApplyVolumes();
    }

    public void SetMuted(bool value)
    {
        Muted = value;
        PlayerPrefs.SetInt(SettingsPrefix + "Muted", value ? 1 : 0);
        settingsDirty = true;
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        if (!initialized) return;
        ApplyVolume(music, BgmVolume);
        foreach (Voice voice in effects) ApplyVolume(voice, SfxVolume);
        foreach (Voice voice in ui) ApplyVolume(voice, UiVolume);
    }

    private void ApplyVolume(Voice voice, float channelVolume)
    {
        voice.Source.volume = MasterVolume * channelVolume * voice.Gain;
        voice.Source.mute = Muted;
    }

    // Call on closing a volume settings panel; also saved on pause/quit/destruction.
    public void SaveSettings()
    {
        if (!settingsDirty) return;
        PlayerPrefs.Save();
        settingsDirty = false;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        StopSfx(); StopUi();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveSettings();
    }

    private void OnApplicationQuit()
    {
        if (instance != this) return;
        quitting = true;
        SaveSettings();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SaveSettings();
        instance = null;
    }
}
