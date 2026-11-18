using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 검증 복사본 전용. 실제 씬에서 가상 키보드·마우스로 도발을 조준·클릭해 시전하고,
// 거대화·끌어오기·종료 후 복구·HUD 슬롯·웨이브 완료를 확인한다.
// 실행: TAUNT_SCENE=<씬 경로> TAUNT_OUT=<출력 폴더> Unity -batchmode -projectPath <복사본> -executeMethod TauntPlayValidation.Run
[InitializeOnLoad]
internal static class TauntPlayValidation
{
    const string RunningKey = "TauntPlayValidation.Running";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly string[] SlotNames = { "Attack Buff", "Heal", "Move Speed Buff", "Armor Buff", "Taunt" };

    static VoxelDefensePrototype flow;
    static DefenseBattle battle;
    static DefenseBoardView view;
    static Camera camera;
    static UIManager ui;
    static Mouse mouse;
    static Keyboard keyboard;
    static int stage, waitFrames, castTick, captures;
    static double started;
    static EnemyUnit taunter;
    static List<EnemyUnit> pulled = new();
    static HashSet<EnemyUnit> engagedElsewhere = new(), hiddenByWall = new(), turned = new(), struck = new();
    // 시전 직전 상태. 시전 틱 안에서 바로 표적이 바뀌므로 미리 기록한다.
    static HashSet<EnemyUnit> preEngaged = new(), preHidden = new();
    static float peakScale, baseMaxHealth;
    static Vector2 pointer;
    static readonly List<string> errors = new();
    static readonly List<string> report = new();

    static string Scene => Environment.GetEnvironmentVariable("TAUNT_SCENE") ?? "Assets/01_Scenes/02_Gameplay/GameScene.unity";
    static string Out => Environment.GetEnvironmentVariable("TAUNT_OUT") ?? "/tmp/taunt-validation";

    static TauntPlayValidation() { EditorApplication.update += Update; }

    public static void Run()
    {
        EditorSceneManager.OpenScene(Scene);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static object Field(object owner, string name) => owner.GetType().GetField(name, Any).GetValue(owner);
    static void Call(string name, params object[] args) =>
        typeof(VoxelDefensePrototype).GetMethod(name, Any).Invoke(flow, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Log(string line) { report.Add(line); Debug.Log("TAUNT_VALIDATION " + line); }

    static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (waitFrames > 0) { waitFrames--; return; }
            switch (stage)
            {
                case 0: Setup(); break;
                case 1: FindTarget(); break;
                case 2: PressKey(Key.Digit5, true); stage++; waitFrames = 2; break;
                case 3: PressKey(Key.Digit5, false); stage++; waitFrames = 2; break;
                case 4: AimAtTarget(); break;
                case 10: VerifyAim(); break;
                case 5: Click(true); stage++; waitFrames = 2; break;
                case 6: Click(false); stage++; waitFrames = 2; break;
                case 7: ConfirmCast(); break;
                case 8: TrackTaunt(); break;
                case 9: FinishWave(); break;
            }
            if (EditorApplication.timeSinceStartup - started > 300) throw new Exception($"Timed out at stage {stage}.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(1, e.Message); }
    }

    static void Setup()
    {
        flow = UnityEngine.Object.FindFirstObjectByType<VoxelDefensePrototype>();
        if (flow == null) return;
        battle = (DefenseBattle)Field(flow, "battle");
        if (battle == null) return;
        view = (DefenseBoardView)Field(flow, "view");
        camera = (Camera)Field(flow, "boardCamera");
        ui = (UIManager)Field(flow, "ui");
        started = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert && !message.StartsWith("TAUNT_VALIDATION"))
                errors.Add($"{type}: {message}");
        };
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>("Validation Mouse");
        keyboard = InputSystem.AddDevice<Keyboard>("Validation Keyboard");
        mouse.MakeCurrent();
        keyboard.MakeCurrent();
        Directory.CreateDirectory(Out);
        Log($"scene={Scene} screen={camera.pixelWidth}x{camera.pixelHeight} popupOpen={ui.HasOpenPopup}");
        CheckHudSlots();
        Call("StartBattle");
        Check(battle.CurrentPhase == DefenseBattle.Phase.Combat, "The battle did not start.");
        stage = 1;
    }

    // 다섯 슬롯이 겹치지 않고 화면 안에 있으며 횟수 표시가 없다.
    static void CheckHudSlots()
    {
        var all = ui.HudLayer.GetComponentsInChildren<RectTransform>(true);
        var slots = SlotNames.Select(name => all.FirstOrDefault(rect => rect.name == name && rect.parent.name == "Spell Bar")).ToArray();
        Check(slots.All(slot => slot != null), "A spell slot is missing.");
        Check(all.All(rect => rect.name != "Charges"), "A use-count label is still created.");
        for (int i = 1; i < slots.Length; i++)
            Check(slots[i].anchoredPosition.x - slots[i - 1].anchoredPosition.x >= slots[i].sizeDelta.x, "Spell slots overlap.");
        Log("hud slots=" + string.Join(", ", slots.Select(s => $"{s.name}@{s.anchoredPosition.x:0}")));
    }

    static void FindTarget()
    {
        if (battle.CurrentPhase != DefenseBattle.Phase.Combat) throw new Exception("The wave ended before a taunt target appeared.");
        if (battle.Mana < DefenseBattle.ManaCost(DefenseBattle.Spell.Taunt)) return;
        var grid = (DefenseGrid)Field(flow, "grid");
        EnemyUnit best = null;
        int bestScore = 0;
        foreach (EnemyUnit unit in battle.Enemies)
        {
            if (!OnBoardScreen(unit, out _)) continue;
            var reach = battle.Enemies.Where(other => DefenseBattle.IsTauntable(unit, other)).ToList();
            if (reach.Count == 0) continue;
            int score = reach.Count + 10 * reach.Count(o => o.Target != null && o.Target != unit) +
                10 * reach.Count(o => !grid.HasLineOfSight(o.Position, unit.Position));
            if (score > bestScore) { best = unit; bestScore = score; }
        }
        // 교전 중이거나 벽 너머 유닛을 끌어오는 경우를 잠시 기다려 본다.
        bool interesting = bestScore >= 10;
        if (best == null || (!interesting && battle.TickIndex < 60 * 40)) return;
        taunter = best;
        foreach (EnemyUnit other in battle.Enemies.Where(o => DefenseBattle.IsTauntable(best, o)))
        {
            if (other.Target != null && other.Target != best) preEngaged.Add(other);
            if (!grid.HasLineOfSight(other.Position, best.Position)) preHidden.Add(other);
        }
        baseMaxHealth = taunter.MaxHealth;
        Log($"target spawn={taunter.SpawnOrder} faction={taunter.FactionIndex} tick={battle.TickIndex} score={bestScore}");
        stage = 2;
    }

    static bool OnBoardScreen(EnemyUnit unit, out Vector2 screen)
    {
        Vector3 point = view.ScreenPoint(unit.Position + Vector3.up * 0.15f);
        screen = point;
        return point.z > 0 && camera.pixelRect.Contains(screen) && !ui.BlocksBoardPointer(screen);
    }

    static void PressKey(Key key, bool down) =>
        InputSystem.QueueStateEvent(keyboard, down ? new KeyboardState(key) : new KeyboardState());

    static void MoveMouse(Vector2 position, bool pressed) =>
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });

    static void AimAtTarget()
    {
        Check((DefenseBattle.Spell?)Field(flow, "aimedSpell") == DefenseBattle.Spell.Taunt, "Key 5 did not start taunt aiming.");
        if (!taunter.IsAlive) throw new Exception("The chosen target died while aiming.");
        Check(OnBoardScreen(taunter, out pointer), "The target left the visible board.");
        // 화면에 그려진 위치로 조준한다(틱 사이 보간).
        var views = (IDictionary)Field(view, "enemyViews");
        var root = (Transform)Field(views[taunter], "Root");
        pointer = camera.WorldToScreenPoint(root.position + root.up * 0.15f);
        MoveMouse(pointer, false);
        waitFrames = 3;
        stage = 10;
    }

    static void VerifyAim()
    {
        Check(view.TryPickEnemy(pointer, battle.Enemies, out EnemyUnit hovered) && hovered == taunter,
            $"Pointing at the target picked {(hovered == null ? "nothing" : "spawn " + hovered.SpawnOrder)}.");
        Capture("1-aiming", withHud: true);
        captures = 1;
        stage = 5;
    }

    static void Click(bool down) => MoveMouse(pointer, down);

    static void ConfirmCast()
    {
        var taunts = battle.CommandHistory.Where(c => c.Spell == DefenseBattle.Spell.Taunt).ToList();
        if (taunts.Count == 0)
        {
            if (castTick == 0) castTick = Math.Max(1, battle.TickIndex);
            if (battle.TickIndex - castTick > 120) throw new Exception("The click did not cast taunt.");
            return;
        }
        Check(taunts[0].TargetSpawnOrder == taunter.SpawnOrder, "Taunt hit a different unit.");
        Check((DefenseBattle.Spell?)Field(flow, "aimedSpell") == null, "Aiming did not end after the cast.");
        castTick = taunts[0].Tick;
        Check(taunter.IsTaunting, "The target is not taunting.");
        Check(taunter.MaxHealth == baseMaxHealth + Mathf.CeilToInt(taunter.Faction.MaxHealth * EnemyUnit.TauntHealthBonusPercent),
            "Max health did not grow.");
        var grid = (DefenseGrid)Field(flow, "grid");
        pulled = battle.Enemies.Where(unit => unit.ActiveTaunter == taunter).ToList();
        foreach (EnemyUnit unit in pulled)
        {
            if (preEngaged.Contains(unit)) engagedElsewhere.Add(unit);
            if (preHidden.Contains(unit)) hiddenByWall.Add(unit);
        }
        Log($"cast tick={castTick} maxHealth={baseMaxHealth}->{taunter.MaxHealth} pulled={pulled.Count} " +
            $"(engagedElsewhere≈{engagedElsewhere.Count}, behindWall={hiddenByWall.Count})");
        typeof(VoxelDefensePrototype).GetField("battleSpeed", Any).SetValue(flow, 1);
        stage = 8;
    }

    static void TrackTaunt()
    {
        var views = (IDictionary)Field(view, "enemyViews");
        float scale = 1f;
        if (taunter.IsAlive && views.Contains(taunter))
            scale = (float)Field(views[taunter], "GiantScale");
        peakScale = Mathf.Max(peakScale, scale);
        foreach (EnemyUnit unit in pulled)
        {
            if (unit.Target == taunter) turned.Add(unit);
            if (unit.LastAttack.Target == taunter && unit.AttacksPerformed > 0) struck.Add(unit);
        }
        int elapsed = battle.TickIndex - castTick;
        if (captures == 1 && elapsed >= 90)
        {
            Capture("2-giant", withHud: true);
            if (taunter.IsAlive) Capture("2-giant-zoom", withHud: false, focus: taunter.Position);
            captures = 2;
        }
        if (elapsed < DefenseBattle.TauntDuration * DefenseBattle.TicksPerSecond + 60 && taunter.IsAlive &&
            battle.CurrentPhase == DefenseBattle.Phase.Combat)
            return;

        Check(peakScale >= 1.99f, $"The taunter never grew (peak {peakScale:0.00}).");
        Log($"taunt over: taunterAlive={taunter.IsAlive} peakScale={peakScale:0.00} turned={turned.Count}/{pulled.Count} " +
            $"struckTaunter={struck.Count} engagedElsewhereTurned={engagedElsewhere.Count(turned.Contains)}/{engagedElsewhere.Count} " +
            $"behindWallTurned={hiddenByWall.Count(turned.Contains)}/{hiddenByWall.Count}");
        if (pulled.Count > 0) Check(turned.Count > 0, "No pulled unit ever targeted the taunter.");
        if (taunter.IsAlive)
        {
            Check(!taunter.IsTaunting && taunter.MaxHealth == baseMaxHealth, "Taunt did not wear off.");
            Check(pulled.All(unit => unit.ActiveTaunter == null), "A unit stayed taunted.");
            waitFrames = 30;
            stage = 9;
            Log("restored: maxHealth back, no unit still taunted");
        }
        else stage = 9;
        typeof(VoxelDefensePrototype).GetField("battleSpeed", Any).SetValue(flow, 2);
    }

    static void FinishWave()
    {
        if (captures == 2 && taunter.IsAlive)
        {
            var views = (IDictionary)Field(view, "enemyViews");
            float scale = (float)Field(views[taunter], "GiantScale");
            if (scale > 1f) return;
            Capture("3-after-zoom", withHud: false, focus: taunter.Position);
            captures = 3;
            Log("giant scale back to 1");
        }
        if (battle.CurrentPhase == DefenseBattle.Phase.Combat) return;
        Log($"wave finished: phase={battle.CurrentPhase} core={battle.CoreHealth} remaining={battle.Enemies.Count}");
        Check(battle.CurrentPhase != DefenseBattle.Phase.Defeat || battle.CoreHealth == 0, "Unexpected defeat state.");
        Finish(0, null);
    }

    // 보드 카메라를 텍스처에 렌더한다. withHud 면 잠시 캔버스를 카메라 공간으로 바꿔 HUD 도 함께 그린다.
    static void Capture(string name, bool withHud, Vector3? focus = null)
    {
        Vector3 oldPosition = camera.transform.position;
        float oldSize = camera.orthographicSize;
        if (focus is { } point)
        {
            camera.transform.position = flow.transform.TransformPoint(point) - camera.transform.forward * 20f;
            camera.orthographicSize = 2.6f;
        }
        const int width = 1600, height = 900;
        var rt = new RenderTexture(width, height, 24);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        Canvas canvas = ui.GetComponent<Canvas>();
        RenderMode mode = canvas.renderMode;
        Camera canvasCamera = canvas.worldCamera;
        float plane = canvas.planeDistance;
        camera.targetTexture = rt;
        if (withHud)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.5f;
            Canvas.ForceUpdateCanvases();
        }
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Out, name + ".png"), texture.EncodeToPNG());
        canvas.renderMode = mode;
        canvas.worldCamera = canvasCamera;
        canvas.planeDistance = plane;
        camera.targetTexture = previous;
        RenderTexture.active = active;
        camera.transform.position = oldPosition;
        camera.orthographicSize = oldSize;
        UnityEngine.Object.Destroy(texture);
        rt.Release();
        UnityEngine.Object.Destroy(rt);
        Log("captured " + name);
    }

    static void Finish(int code, string failure)
    {
        if (failure != null) report.Add("FAIL: " + failure);
        report.Add(errors.Count == 0 ? "errors: none" : "errors:\n  " + string.Join("\n  ", errors));
        try { Directory.CreateDirectory(Out); File.WriteAllLines(Path.Combine(Out, "summary.txt"), report); } catch { }
        SessionState.SetBool(RunningKey, false);
        EditorApplication.Exit(code);
    }
}
