using System;
using UnityEngine;

// 종족 = 전투 진영. 순서가 StageData.Spawn.faction 과 라운드 편성의 진영 번호다.
internal enum EnemyFactionGroup
{
    GreenHorde,
    Undead,
    Beasts,
    DragonAndDemon
}

[CreateAssetMenu(menuName = "BeViolence Defense/Enemy Catalog")]
public sealed class EnemyCatalog : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        [SerializeField] private EnemyFactionGroup factionGroup;
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject model;
        [SerializeField] private RuntimeAnimatorController animations;
        [SerializeField] private Material skin;
        [SerializeField, Min(0.01f)] private float modelScale = 1f;

        [SerializeField, Min(0.05f), Tooltip("모델 배율 적용 전 몸통 점유 반경(칸). 무기·날개 끝은 제외합니다.")]
        private float bodyRadius = 0.3f;

        [SerializeField, Min(1)] private int maxHealth = 50;
        [SerializeField, Min(0)] private int damage = 10;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.65f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(0.1f)] private float awarenessRange = 3f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 1.7f;
        [SerializeField, Min(0)] private int armor;

        public string Id => id;
        public string DisplayName => displayName;
        public EnemyFactionGroup FactionGroup => factionGroup;

        public EnemyFaction CreateFaction(string factionName, Color color, int spawnCount) =>
            new(factionName, color, attackRange, maxHealth, damage, attackInterval, spawnCount,
                awarenessRange, moveSpeed, armor, id, bodyRadius * modelScale);

        public UnitVisual Visual => new(model, animations, skin, modelScale);
    }

    // 화면 전용 데이터. 전투 로직(EnemyFaction)에는 넣지 않고, 화면이 유닛 ID로 찾아 쓴다.
    internal readonly struct UnitVisual
    {
        public UnitVisual(GameObject model, RuntimeAnimatorController animations, Material skin, float modelScale)
        {
            Model = model;
            Animations = animations;
            Skin = skin;
            ModelScale = modelScale;
        }

        public GameObject Model { get; }
        public RuntimeAnimatorController Animations { get; }
        public Material Skin { get; }
        public float ModelScale { get; }
    }

    internal bool TryGetVisual(string unitId, out UnitVisual visual)
    {
        foreach (Entry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(unitId) || entry.Id != unitId)
                continue;
            visual = entry.Visual;
            return true;
        }
        visual = default;
        return false;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    internal bool TryGetFactionGroup(string unitId, out EnemyFactionGroup group)
    {
        foreach (Entry entry in entries)
        {
            if (entry.Id != unitId)
                continue;
            group = entry.FactionGroup;
            return true;
        }
        group = default;
        return false;
    }

    internal EnemyFaction GetFaction(string unitId, string factionName, Color color, int spawnCount) =>
        Find(unitId, factionName)?.CreateFaction(factionName, color, spawnCount);

    // 라운드 편성용: 진영(편) 색을 입힌 유닛 종류 하나. 이름은 카탈로그의 표시 이름을 쓴다.
    internal EnemyFaction GetUnit(string unitId, Color sideColor, string context)
    {
        Entry entry = Find(unitId, context);
        if (entry == null)
            return null;
        string name = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Id : entry.DisplayName;
        return entry.CreateFaction(name, sideColor, 0);
    }

    private Entry Find(string unitId, string context)
    {
        Entry match = null;
        foreach (Entry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(unitId) || entry.Id != unitId)
                continue;
            if (match != null)
            {
                Debug.LogError($"{name}: duplicate enemy ID '{unitId}'.", this);
                return null;
            }
            match = entry;
        }

        if (match != null)
            return match;

        Debug.LogError($"{name}: enemy ID '{unitId}' was not found for {context}.", this);
        return null;
    }
}
