using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// 전투 로직 파일이 시간·난수·씬 오브젝트·화면 코드에 기대지 않는지 검사한다.
// 결정론과 BalanceSim 이 같은 코드를 쓸 수 있는 전제를 지키기 위한 것이다. 규칙은 docs/determinism.md.
public sealed class SimulationPurityTests
{
    private static readonly string[] SimulationFiles =
    {
        "02_Scripts/00_Core/BattleClock.cs",
        "02_Scripts/00_Core/CombatPositioning.cs",
        "02_Scripts/03_Enemies/EnemyUnit.cs",
        "02_Scripts/04_Combat/DefenseBattle.cs",
        "02_Scripts/05_World/DefenseGrid.cs",
    };

    // 이름: 금지 이유
    private static readonly (string Pattern, string Reason)[] Forbidden =
    {
        (@"\bTime\s*\.", "프레임 시간은 BattleClock 이 틱으로 바꿔 전달한다"),
        (@"\bRandom\b", "난수가 필요하면 시드를 정한 전투 전용 난수를 쓴다"),
        (@"\bTransform\b|\.transform\b", "씬 오브젝트 위치는 화면 쪽 데이터다"),
        (@"\bAnimator\b|\bAnimation\b", "애니메이션 진행은 화면 쪽에서만 본다"),
        (@"\bGameObject\b|\bMaterial\b|\bRenderer\b|\bRuntimeAnimatorController\b", "화면 데이터는 EnemyCatalog.UnitVisual 에 둔다"),
        (@"\bMonoBehaviour\b|\bFindObject", "전투 로직은 씬에 의존하지 않는다"),
        (@"\bPhysics\b|\bRaycast\b", "물리 엔진 결과는 프레임에 따라 달라질 수 있다"),
        (@"\b(DefenseBoardView|DefenseHud|UIManager|UIPalette|HitEffect|SpellEffects|SpellCastMarkers|BoardTheme|VoxelDefensePrototype|EnemyCatalog)\b",
            "전투 로직은 화면·UI·씬 코드를 참조하지 않는다"),
    };

    [Test]
    public void SimulationFilesAvoidFrameAndSceneDependencies()
    {
        var violations = (from file in SimulationFiles
                          let path = Path.Combine(Application.dataPath, file)
                          let code = StripCommentsAndStrings(ReadOrFail(path, file))
                          from rule in Forbidden
                          from Match match in Regex.Matches(code, rule.Pattern)
                          select $"{file}:{LineOf(code, match.Index)}  '{match.Value}' — {rule.Reason}").ToList();

        Assert.That(violations, Is.Empty, "전투 로직이 금지된 API를 쓴다:\n" + string.Join("\n", violations));
    }

    private static string ReadOrFail(string path, string file)
    {
        if (!File.Exists(path))
            Assert.Fail($"{file} 를 찾지 못했다. 전투 로직 파일을 옮겼다면 SimulationFiles 목록도 고친다.");
        return File.ReadAllText(path);
    }

    // 주석과 문자열 안의 단어는 검사하지 않는다. 줄 번호를 유지하려고 줄바꿈은 남긴다.
    private static string StripCommentsAndStrings(string code) =>
        Regex.Replace(code, @"//[^\n]*|/\*.*?\*/|@""(?:""""|[^""])*""|\$?""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])'",
            m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);

    private static int LineOf(string code, int index) => code.Take(index).Count(c => c == '\n') + 1;
}
