using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StageSelectionController : MonoBehaviour
{
    [SerializeField] private StageContent[] stages;
    [SerializeField] private VoxelDefensePrototype game;
    [SerializeField] private UIManager ui;
    [SerializeField] private StageSelectionPopup popup;
    [SerializeField] private GameObject selectionCamera;
    [Tooltip("선택. 있으면 입장할 때 막으로 화면을 덮은 동안 스테이지를 만든다.")]
    [SerializeField] private StageTransition transition;
    private static int retryStage = -1;
    private bool entering;
    private bool startCovered;

    public int SelectedStage { get; private set; } = -1;
    public StageContent CurrentStage { get; private set; }
    public int StageCount => stages.Length;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => retryStage = -1;

    private void Start()
    {
        popup.Bind(stages, SelectStage);
        int pending = retryStage;
        retryStage = -1;
        if (pending >= 0 && pending < stages.Length)
        {
            startCovered = true;
            SelectStage(pending);
        }
        else
            ui.ShowPopup((RectTransform)popup.transform, dismissOnEscape: false);
    }

    public void SelectStage(int index)
    {
        if (entering || SelectedStage >= 0 || index < 0 || index >= stages.Length || stages[index] == null)
            return;
        StageContent prefab = stages[index];
        if (prefab.Data == null || prefab.BoardCamera == null || prefab.Data.Validate(prefab.Data.CreateGrid()) != null)
        {
            Debug.LogError($"Stage prefab '{prefab.name}' has invalid board settings or no camera.", prefab);
            return;
        }
        entering = true;
        if (transition != null)
            transition.Play(index + 1, prefab.DisplayName, () => Enter(index, prefab), startCovered);
        else
            Enter(index, prefab);
    }

    private void Enter(int index, StageContent prefab)
    {
        selectionCamera.SetActive(false);
        CurrentStage = Instantiate(prefab);
        CurrentStage.name = prefab.name;
        CurrentStage.ApplyEnvironment();
        SelectedStage = index;
        if (ui.HasOpenPopup)
            ui.CloseTopPopup();
        game.InitializeStage(CurrentStage, index + 1, Retry);
    }

    private void Retry()
    {
        retryStage = SelectedStage;
        SceneManager.LoadScene(gameObject.scene.buildIndex);
    }
}
