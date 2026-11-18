internal sealed class BattleClock
{
    public const float AimSpeed = 0.1f;

    public float StepDeltaTime { get; private set; }
    public int StepCount { get; private set; }
    public float PlaybackSpeed { get; private set; } = 1f;

    // The elapsed gameplay time for effects and other systems updated once per frame.
    public float DeltaTime => StepDeltaTime * StepCount;

    public void BeginFrame(float frameDeltaTime, int battleSpeed, bool aiming)
    {
        PlaybackSpeed = aiming ? AimSpeed : battleSpeed;
        StepDeltaTime = aiming ? frameDeltaTime * AimSpeed : frameDeltaTime;
        StepCount = StepDeltaTime > 0f ? (aiming ? 1 : battleSpeed) : 0;
    }
}
