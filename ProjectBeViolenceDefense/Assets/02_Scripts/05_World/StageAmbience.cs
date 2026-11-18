using UnityEngine;

/// <summary>
/// 스테이지 분위기 연출: 화면에 떠다니는 입자(낙엽·눈)와, 유닛이 쓰러진 자리에서 한 번 흩날리는 입자.
/// 씬에 놓인 파티클을 연결해 쓰며, VoxelDefensePrototype에 이 컴포넌트가 연결된 씬에서만 동작한다.
/// 전투 규칙에는 관여하지 않는다(보기 연출만).
/// </summary>
public sealed class StageAmbience : MonoBehaviour
{
    [Tooltip("항상 떠다니는 입자 (낙엽, 눈)")]
    [SerializeField] private ParticleSystem ambient;
    [Tooltip("유닛이 쓰러진 자리에서 한 번 터지는 입자")]
    [SerializeField] private ParticleSystem burst;
    [SerializeField, Min(0)] private int burstCount = 12;
    [SerializeField] private float burstHeight = 0.35f;
    [Tooltip("켜면 평평한 보드 바닥 타일이 그림자를 드리우지 않게 한다 (받기는 한다). 그림자 맵 부하를 줄인다.")]
    [SerializeField] private bool flatBoardShadows = true;

    private void Start()
    {
        if (!flatBoardShadows)
            return;
        // 보드는 시작할 때 코드로 만들어지므로 여기서 이름으로 찾아 끈다. 벽·유닛 그림자는 그대로 둔다.
        foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string name = renderer.name;
            if (name.StartsWith("Tile ") || name.StartsWith("Board base") || name.ToLowerInvariant().Contains("spawn pad") || name.StartsWith("Border"))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>유닛이 사라진 위치에서 입자를 한 번 흩날린다.</summary>
    public void Burst(Vector3 position)
    {
        if (burst == null || burstCount == 0)
            return;
        var emit = new ParticleSystem.EmitParams { position = position + Vector3.up * burstHeight, applyShapeToPosition = true };
        burst.Emit(emit, burstCount);
    }
}
