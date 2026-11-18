using UnityEngine;

/// <summary>
/// 화면 구석 전경 장식(나뭇가지 등)을 바람에 흔들리듯 피벗 기준으로 천천히 회전시킨다. 보기 연출만 한다.
/// </summary>
public sealed class SwayingSprite : MonoBehaviour
{
    [SerializeField] private float angle = 3.5f;
    [SerializeField] private float speed = 0.9f;
    [SerializeField] private float phase;

    private RectTransform rect;
    private float baseAngle;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseAngle = rect.localEulerAngles.z;
    }

    private void Update()
    {
        float t = Time.unscaledTime * speed + phase;
        // 두 박자를 섞어 기계적인 왕복처럼 보이지 않게 한다.
        float sway = Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.3f + 1.7f) * 0.3f;
        rect.localRotation = Quaternion.Euler(0f, 0f, baseAngle + sway * angle);
    }
}
