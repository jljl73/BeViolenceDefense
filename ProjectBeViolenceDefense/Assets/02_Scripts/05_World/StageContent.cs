using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One stage prefab: board settings, camera, lights, volume and decorative environment.</summary>
public sealed class StageContent : MonoBehaviour
{
    [SerializeField] private string displayNameKey;
    [SerializeField] private StageData stage;
    [SerializeField] private BoardTheme theme = new();
    [SerializeField] private Camera boardCamera;
    [SerializeField] private StageAmbience ambience;
    [SerializeField] private Light sun;
    [SerializeField] private Material skybox;
    [SerializeField] private AmbientMode ambientMode;
    [SerializeField] private Color ambientSky;
    [SerializeField] private Color ambientEquator;
    [SerializeField] private Color ambientGround;
    [SerializeField] private float ambientIntensity = 1f;
    [SerializeField] private float reflectionIntensity = 1f;
    [SerializeField] private bool fog;
    [SerializeField] private Color fogColor;
    [SerializeField] private FogMode fogMode;
    [SerializeField] private float fogDensity;
    [SerializeField] private float fogStart;
    [SerializeField] private float fogEnd;

    public string DisplayName => GameText.Get(displayNameKey);
    public StageData Data => stage;
    public BoardTheme Theme => theme;
    public Camera BoardCamera => boardCamera;
    public StageAmbience Ambience => ambience;

    public void ApplyEnvironment()
    {
        RenderSettings.sun = sun;
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }
}
