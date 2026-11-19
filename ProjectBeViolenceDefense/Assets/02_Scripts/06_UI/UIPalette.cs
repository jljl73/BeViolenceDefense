using UnityEngine;

/// <summary>
/// UI 색상 규칙. (2026-11-18 주인공 기준 팔레트: 블라우스 크림, 금발 꿀색, 지팡이 갈색에서 뽑은 따뜻한 톤)
/// UI 색상 규칙. UI 코드에서는 색을 직접 쓰지 말고 이 토큰만 사용한다.
/// 색을 바꿀 때는 이 파일의 hex 값만 고치면 전체 UI 에 반영된다.
///
/// 규칙
///  - 깊이: Background → Panel → PanelRaised 순으로 밝아진다. 위에 올라오는 요소일수록 한 단계 밝은 면을 쓴다.
///  - PanelBrown 은 보상·경고·팝업 헤더처럼 시선을 끌어야 하는 영역에만 쓴다.
///  - 외곽선은 면보다 한 단계 밝게. 선택·포커스만 OutlineFocus(금색)를 쓴다.
///  - 밝은 버튼(금색·스킬색) 위 글자는 TextOnAccent, 어두운 면 위 글자는 TextPrimary/TextSecondary.
///  - 게임 의미 색(마나·공격·회복·이속)은 UI 에서 해당 기능을 가리킬 때만 쓴다.
/// </summary>
public static class UIPalette
{
    // ── 배경과 패널 ─────────────────────────────
    public static readonly Color Background = Hex("#17121F");
    public static readonly Color Panel = Hex("#2A2030");
    public static readonly Color PanelRaised = Hex("#3B2D3D");
    public static readonly Color PanelBrown = Hex("#5E3F3C");
    public static readonly Color Overlay = Hex("#17121FB8");     // 모달 뒤 화면 (Background 72%)
    public static readonly Color BarTrack = Hex("#150F1A");      // 게이지 빈 부분

    // ── 외곽선 ─────────────────────────────────
    public static readonly Color Outline = Hex("#7D6654");       // 패널·버튼 기본 테두리
    public static readonly Color OutlineStrong = Hex("#C9A77C"); // 호버, 갈색 패널 테두리
    public static readonly Color OutlineFocus = Hex("#FFD98A");  // 선택·포커스

    // ── 텍스트 ─────────────────────────────────
    public static readonly Color TextPrimary = Hex("#F6EDE6");   // 본문, 제목
    public static readonly Color TextSecondary = Hex("#C7B6AD"); // 보조 설명, 비활성 버튼 글자
    public static readonly Color TextDisabled = Hex("#7A6A6C");  // 완전히 꺼진 항목
    public static readonly Color TextOnAccent = Hex("#2A2030");  // 밝은 버튼 위 글자·아이콘

    // ── 버튼 ───────────────────────────────────
    public static readonly Color ButtonPrimary = Hex("#FFD27A");   // 시작, 재시작 같은 주요 행동
    public static readonly Color ButtonSecondary = Hex("#5A4650"); // 일반 행동 (글자는 TextPrimary)
    public static readonly Color ButtonDisabled = Hex("#3E3138");  // 사용 불가, 미선택
    public static readonly Color ButtonDanger = Hex("#E5484D");    // 삭제, 포기 같은 되돌릴 수 없는 행동

    // ── 밝은 화면 (스테이지 선택처럼 배경이 비치는 화면) ──
    public static readonly Color LightSurface = Hex("#FFF8EEE6");     // 크림색 반투명 카드·상단 바
    public static readonly Color LightSurfaceLine = Hex("#E6CCA4");   // 카드의 얇은 테두리
    public static readonly Color LightVeil = Hex("#FFF4E659");        // 배경 위에 덮는 옅은 크림 막
    public static readonly Color TextOnLight = Hex("#3B2D3D");        // 크림 면 위 글자
    public static readonly Color TextOnLightMuted = Hex("#9C8A86");   // 크림 면 위 보조·잠김 글자
    public static readonly Color ChipOnLight = Hex("#FFE3A3");        // 크림 면 위 상태 꼬리표
    public static readonly Color Shadow = Hex("#2A203059");           // 떠 있는 카드의 그림자

    // ── 게임 의미 색 ────────────────────────────
    public static readonly Color Mana = Hex("#4BDEFB");
    public static readonly Color Attack = Hex("#FFA588");
    public static readonly Color Heal = Hex("#99F5A0");
    public static readonly Color MoveSpeed = Hex("#66DAFF");
    public static readonly Color Armor = Hex("#C5A7FF");
    public static readonly Color CoreShield = Hex("#92B7FF");
    public static readonly Color Taunt = Hex("#FF7FD1");

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
}
