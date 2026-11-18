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
}
