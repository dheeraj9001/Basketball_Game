using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HoldShootButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public BasketballCanvasUI ui;

    public void OnPointerDown(PointerEventData eventData) => ui.BeginShotCharge();
    public void OnPointerUp(PointerEventData eventData) => ui.ReleaseShotCharge();
    public void OnPointerExit(PointerEventData eventData) => ui.ReleaseShotCharge();
}
