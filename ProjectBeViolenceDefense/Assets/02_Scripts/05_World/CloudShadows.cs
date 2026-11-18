using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 방향광의 쿠키(구름 무늬)를 천천히 흘려 구름 그림자가 지나가는 것처럼 보이게 한다. 보기 연출만 한다.
/// </summary>
[RequireComponent(typeof(Light))]
public sealed class CloudShadows : MonoBehaviour
{
    [Tooltip("구름 무늬 한 장이 덮는 넓이 (월드 단위)")]
    [SerializeField] private Vector2 size = new(46f, 46f);
    [Tooltip("구름이 흐르는 속도 (무늬 비율/초)")]
    [SerializeField] private Vector2 speed = new(0.012f, 0.005f);

    private UniversalAdditionalLightData data;
    private Vector2 offset;

    private void Awake()
    {
        data = GetComponent<UniversalAdditionalLightData>();
        if (data != null)
            data.lightCookieSize = size;
    }

    private void Update()
    {
        if (data == null)
            return;
        offset += speed * Time.deltaTime;
        offset.x -= Mathf.Floor(offset.x);
        offset.y -= Mathf.Floor(offset.y);
        data.lightCookieOffset = new Vector2(offset.x * size.x, offset.y * size.y);
    }
}
