using UnityEngine;

/// <summary>
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
    public static readonly Color Background = Hex("#0B0D2B");
    public static readonly Color Panel = Hex("#1D1A38");
    public static readonly Color PanelRaised = Hex("#32273F");
    public static readonly Color PanelBrown = Hex("#583642");
    public static readonly Color Overlay = Hex("#0B0D2BB8");     // 모달 뒤 화면 (Background 72%)
    public static readonly Color BarTrack = Hex("#120F26");      // 게이지 빈 부분

    // ── 외곽선 ─────────────────────────────────
    public static readonly Color Outline = Hex("#4A3B5C");       // 패널·버튼 기본 테두리
    public static readonly Color OutlineStrong = Hex("#7A5A78"); // 호버, 갈색 패널 테두리
    public static readonly Color OutlineFocus = Hex("#FFD170");  // 선택·포커스

    // ── 텍스트 ─────────────────────────────────
    public static readonly Color TextPrimary = Hex("#F3E9E2");   // 본문, 제목
    public static readonly Color TextSecondary = Hex("#B9A9BE"); // 보조 설명, 비활성 버튼 글자
    public static readonly Color TextDisabled = Hex("#6E6280");  // 완전히 꺼진 항목
    public static readonly Color TextOnAccent = Hex("#1D1A38");  // 밝은 버튼 위 글자·아이콘

    // ── 버튼 ───────────────────────────────────
    public static readonly Color ButtonPrimary = Hex("#FFD170");   // 시작, 재시작 같은 주요 행동
    public static readonly Color ButtonSecondary = Hex("#4A3B5C"); // 일반 행동 (글자는 TextPrimary)
    public static readonly Color ButtonDisabled = Hex("#3A3148");  // 사용 불가, 미선택
    public static readonly Color ButtonDanger = Hex("#E5484D");    // 삭제, 포기 같은 되돌릴 수 없는 행동

    // ── 게임 의미 색 ────────────────────────────
    public static readonly Color Mana = Hex("#64C4FF");
    public static readonly Color Attack = Hex("#FFA588");
    public static readonly Color Heal = Hex("#99F5A0");
    public static readonly Color MoveSpeed = Hex("#66DAFF");
    public static readonly Color Armor = Hex("#C5A7FF");

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
}
