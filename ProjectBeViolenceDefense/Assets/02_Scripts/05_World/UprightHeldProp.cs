using UnityEngine;

/// <summary>
/// 손에 든 소품(활 등)을 손 위치에 붙인 채 똑바로 세운다. 다른 캐릭터의 동작을 빌려 쓰면 손목 각도가 맞지 않아
/// 소품이 눕기 때문에, 손 위치는 그대로 따라가고 방향만 "길이 축 = 위, 깊이 축 = 캐릭터 앞"으로 맞춘다.
/// 쓰러지는 동작(<see cref="releaseState"/>) 중에는 손에 붙은 그대로 둔다. 화면 표시 전용이다.
/// </summary>
public sealed class UprightHeldProp : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private Animator animator;
    [Tooltip("소품 메시의 길이 방향 로컬 축")]
    [SerializeField] private Vector3 lengthAxis = Vector3.up;
    [Tooltip("소품 메시에서 캐릭터 앞쪽을 볼 로컬 축")]
    [SerializeField] private Vector3 depthAxis = Vector3.right;
    [Tooltip("이 상태에서는 손 방향을 그대로 따른다")]
    [SerializeField] private string releaseState = "Death";
    [Tooltip("소품이 앞으로 살짝 기울어지는 각도(위에서 내려다보는 카메라에서 서 있는 것처럼 보이게)")]
    [SerializeField] private float forwardLean = 8f;

    private Quaternion handLocal;
    private bool captured;

    private void Awake() => Capture();

    private void Capture()
    {
        if (captured)
            return;
        captured = true;
        handLocal = transform.localRotation;
    }

    private void LateUpdate() => Apply();

    /// <summary>현재 자세에 맞춰 소품 방향을 정한다(에디터 미리보기에서도 부른다).</summary>
    public void Apply()
    {
        Capture();
        bool released = animator != null && animator.isActiveAndEnabled
            && animator.GetCurrentAnimatorStateInfo(0).IsName(releaseState);
        if (released || body == null)
        {
            transform.localRotation = handLocal;
            return;
        }
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 up = Quaternion.AngleAxis(forwardLean, Vector3.Cross(Vector3.up, forward)) * Vector3.up;
        Quaternion target = Quaternion.LookRotation(forward, up);
        Quaternion meshFrame = Quaternion.LookRotation(depthAxis, lengthAxis);
        transform.rotation = target * Quaternion.Inverse(meshFrame);
    }
}
