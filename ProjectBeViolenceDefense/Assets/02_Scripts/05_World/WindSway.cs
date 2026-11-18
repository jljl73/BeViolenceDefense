using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자식 소품(나무)을 바람에 흔들리듯 밑동 기준으로 살짝 기울인다. 바람 방향으로 물결이 지나가듯 위치마다 박자가 다르다.
/// 나무마다 스크립트를 두지 않고 이 컴포넌트 하나가 모두 갱신한다. 보기 연출만 한다.
/// </summary>
public sealed class WindSway : MonoBehaviour
{
    [Tooltip("이 문자열이 이름에 들어 있는 자식만 흔든다. 비우면 모든 자식")]
    [SerializeField] private string nameFilter = "";
    [SerializeField] private Vector3 windDirection = new(1f, 0f, 0.35f);
    [SerializeField] private float angle = 2.2f;
    [SerializeField] private float speed = 1.1f;
    [Tooltip("바람 물결의 촘촘함. 클수록 가까운 나무끼리 박자가 달라진다.")]
    [SerializeField] private float waveScale = 0.32f;
    [Tooltip("가끔 세게 부는 돌풍의 세기 (0이면 일정)")]
    [SerializeField, Range(0f, 1f)] private float gust = 0.5f;

    private readonly List<Transform> items = new();
    private readonly List<Quaternion> baseRotations = new();
    private readonly List<float> phases = new();
    private Vector3 axis;

    private void Awake()
    {
        Vector3 wind = windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector3.right;
        axis = Vector3.Cross(Vector3.up, wind);
        foreach (Transform child in transform)
        {
            if (nameFilter.Length > 0 && !child.name.Contains(nameFilter))
                continue;
            items.Add(child);
            baseRotations.Add(child.rotation);
            phases.Add(Vector3.Dot(child.position, wind) * waveScale);
        }
    }

    private void Update()
    {
        float t = Time.time * speed;
        // 느린 돌풍 주기로 전체 세기를 올렸다 내린다.
        float strength = 1f - gust + gust * (0.5f + 0.5f * Mathf.Sin(t * 0.23f));
        for (int i = 0; i < items.Count; i++)
        {
            float p = t - phases[i];
            float sway = Mathf.Sin(p) * 0.7f + Mathf.Sin(p * 2.3f + 1.1f) * 0.3f;
            // 바람 쪽으로 조금 누운 채 흔들리게 한다.
            float tilt = (0.35f + sway) * angle * strength;
            items[i].rotation = Quaternion.AngleAxis(tilt, axis) * baseRotations[i];
        }
    }
}
