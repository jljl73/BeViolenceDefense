using UnityEngine;

/// <summary>
/// 보드 바닥·벽·배경 색. 기본값은 GameScene의 기존 색이며, 씬마다 인스펙터에서 바꿔 쓴다.
/// </summary>
[System.Serializable]
public sealed class BoardTheme
{
    public Color background = UIPalette.Background;
    public Color foundation = new Color32(40, 54, 69, 255);
    public Color tileA = new Color32(72, 91, 101, 255);
    public Color tileB = new Color32(81, 101, 107, 255);
    public Color routeTile = new Color32(104, 130, 130, 255);
    public Color encounterTile = new Color32(221, 151, 76, 255);
    public Color buildWall = new Color32(112, 143, 182, 255);
    public Color fixedWall = new Color32(97, 120, 136, 255);
    [Tooltip("지정하지 않으면 기본 블록·캡 벽을 사용한다.")]
    public GameObject wallPrefab;
    [Tooltip("나무 블록 프리팹을 고정 벽으로 쓸 때 곱하는 색")]
    public Color fixedWallTint = new Color32(130, 130, 140, 255);
    public Color trim = new Color32(178, 198, 199, 255);

    [Header("코어 표현")]
    [Tooltip("코어 받침대 위에 표시할 캐릭터 프리팹")]
    public GameObject coreCharacterPrefab;
    [Min(0.01f)] public float coreCharacterScale = 1.6f;
    [Tooltip("선택. 지정하면 기본 받침대 블록 대신 이 모델(성채 등)을 코어 자리에 놓는다. 모델 원점은 바닥 중심이어야 한다.")]
    public GameObject corePlatformPrefab;
    [Min(0.01f)] public float corePlatformScale = 1f;
    [Tooltip("받침 모델 위에서 캐릭터가 서는 높이 (corePlatformPrefab을 지정했을 때만 쓴다)")]
    [Min(0f)] public float corePlatformTop = 0.32f;

    [Header("타일 형태")]
    [Tooltip("타일 사이 간격 (칸 크기 1 기준)")]
    [Range(0f, 0.2f)] public float tileGap = 0.06f;
    [Tooltip("타일 윗면 모서리를 깎는 폭. 0이면 기본 큐브를 쓴다.")]
    [Range(0f, 0.15f)] public float tileBevel;
    [Tooltip("바닥 타일·스폰 패드 윗면에 곱하는 질감. 비우면 단색")]
    public Texture2D tileTexture;
    [Tooltip("보드 둘레 테두리 폭. 0이면 테두리를 만들지 않는다.")]
    [Range(0f, 1f)] public float borderWidth;
    public Color border = new Color32(40, 54, 69, 255);
}
