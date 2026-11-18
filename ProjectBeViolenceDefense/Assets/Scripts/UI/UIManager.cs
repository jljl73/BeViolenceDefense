using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class UIManager : MonoBehaviour
{
    [SerializeField] private RectTransform hudLayer;
    [SerializeField] private RectTransform popupLayer;
    [SerializeField] private EventSystem eventSystem;

    private readonly List<(RectTransform Panel, bool DismissOnEscape)> popups = new();
    private readonly List<RaycastResult> pointerHits = new();
    private GraphicRaycaster raycaster;
    private PointerEventData pointerEvent;
    private GameObject modalBackdrop;

    public RectTransform HudLayer => hudLayer;
    public RectTransform PopupLayer => popupLayer;
    public bool HasOpenPopup => popups.Count > 0;

    private void Awake()
    {
        raycaster = GetComponent<GraphicRaycaster>();
        pointerEvent = new PointerEventData(eventSystem);

        var backdrop = new GameObject("Modal Backdrop", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)backdrop.transform;
        rect.SetParent(popupLayer, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = UIPalette.Overlay;
        backdrop.SetActive(false);
        modalBackdrop = backdrop;
    }

    private void Update()
    {
        if (HasOpenPopup && popups[^1].DismissOnEscape && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CloseTopPopup();
    }

    public void ShowPopup(RectTransform panel, bool dismissOnEscape = true)
    {
        if (panel == null || panel.parent != popupLayer)
            throw new System.ArgumentException("Popup panels must be direct children of the Popup layer.", nameof(panel));

        foreach (var popup in popups)
            if (popup.Panel == panel)
                return;

        eventSystem.SetSelectedGameObject(null);
        if (HasOpenPopup)
            popups[^1].Panel.gameObject.SetActive(false);

        popups.Add((panel, dismissOnEscape));
        modalBackdrop.SetActive(true);
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(true);
        panel.GetComponentInChildren<Selectable>()?.Select();
    }

    public bool CloseTopPopup()
    {
        if (!HasOpenPopup)
            return false;

        eventSystem.SetSelectedGameObject(null);
        RectTransform panel = popups[^1].Panel;
        popups.RemoveAt(popups.Count - 1);
        panel.gameObject.SetActive(false);
        if (HasOpenPopup)
        {
            RectTransform previous = popups[^1].Panel;
            previous.gameObject.SetActive(true);
            previous.GetComponentInChildren<Selectable>()?.Select();
        }
        else
            modalBackdrop.SetActive(false);
        return true;
    }

    public bool BlocksBoardPointer(Vector2 screenPosition)
    {
        if (HasOpenPopup)
            return true;

        pointerEvent.Reset();
        pointerEvent.position = screenPosition;
        raycaster.Raycast(pointerEvent, pointerHits);
        bool blocked = pointerHits.Count > 0;
        pointerHits.Clear();
        return blocked;
    }
}
