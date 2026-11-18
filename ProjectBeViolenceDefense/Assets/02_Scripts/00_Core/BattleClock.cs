internal sealed class BattleClock
{
    private bool paused;

    public float StepDeltaTime { get; private set; }
    public int StepCount { get; private set; }

    // The elapsed gameplay time for effects and other systems updated once per frame.
    public float DeltaTime => StepDeltaTime * StepCount;

    public void BeginFrame(float frameDeltaTime, int battleSpeed)
    {
        StepDeltaTime = paused ? 0f : frameDeltaTime;
        StepCount = StepDeltaTime > 0f ? battleSpeed : 0;
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (paused)
        {
            StepDeltaTime = 0f;
            StepCount = 0;
        }
    }
}
