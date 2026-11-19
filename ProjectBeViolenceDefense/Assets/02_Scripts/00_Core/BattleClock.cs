using UnityEngine;

// 전투 시간. 전투 로직은 항상 DefenseBattle.TickDuration(1/60초) 단위로만 진행한다.
// 실제 흐른 시간 x 재생 속도를 쌓아 두었다가 한 틱 분량이 찰 때마다 틱을 돌린다.
// 배속과 조준 슬로우는 틱이 도는 속도만 바꾸므로 전투 결과에는 영향을 주지 않는다.
internal sealed class BattleClock
{
    public const float AimSpeed = 0.1f;
    // 한 진영만 남아 더는 교전이 없을 때 자동으로 쓰는 배속. 첫 테스트용 임시 값.
    public const int FastForwardSpeed = 4;
    // 프레임이 크게 밀려도 한 프레임에 이 이상 틱을 돌리지 않는다. 넘치는 시간은 버려서 게임이 잠시 느려진다.
    public const int MaxTicksPerFrame = 8;

    private float accumulator;

    // 이번 프레임에 돌릴 고정 틱 수와 틱 길이. 시뮬레이션은 이 값으로만 갱신한다.
    public int StepCount { get; private set; }
    public float StepDeltaTime => DefenseBattle.TickDuration;
    public float PlaybackSpeed { get; private set; } = 1f;

    // 화면용 전투 시간(실제 시간 x 재생 속도). 이펙트·애니메이션처럼 프레임당 한 번 부드럽게 갱신하는 곳에 쓴다.
    public float DeltaTime { get; private set; }

    // 직전 틱과 다음 틱 사이 어디쯤인지(0~1). 유닛 위치 보간에 쓴다.
    public float Interpolation => Mathf.Clamp01(accumulator / DefenseBattle.TickDuration);

    public void BeginFrame(float frameDeltaTime, int battleSpeed, bool aiming)
    {
        PlaybackSpeed = aiming ? AimSpeed : battleSpeed;
        DeltaTime = Mathf.Max(0f, frameDeltaTime) * PlaybackSpeed;
        accumulator += DeltaTime;
        StepCount = Mathf.Min(MaxTicksPerFrame, Mathf.FloorToInt(accumulator / DefenseBattle.TickDuration));
        accumulator -= StepCount * DefenseBattle.TickDuration;
        if (StepCount == MaxTicksPerFrame)
            accumulator = Mathf.Min(accumulator, DefenseBattle.TickDuration);
    }

    // 전투를 시작할 때 준비 단계에서 쌓인 시간을 비운다.
    public void Reset()
    {
        accumulator = 0f;
        StepCount = 0;
    }
}
