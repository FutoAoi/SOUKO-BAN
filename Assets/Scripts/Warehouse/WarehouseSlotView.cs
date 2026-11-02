using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Warehouse
{
    public sealed class WarehouseSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
    {
        public Image icon, selection;
        public Text itemName, count, hint;
        public WarehouseGame Game { get; private set; }
        public WarehouseGame.SlotRef Slot { get; private set; }
        public void Bind(WarehouseGame game, System.Collections.Generic.List<Stack> owner, int index, string caption = "")
        {
            Game = game; Slot = new WarehouseGame.SlotRef { owner = owner, index = index }; hint.text = caption; Refresh();
        }
        public void Refresh()
        {
            if (Slot == null || Game.Model == null) return;
            var s = Slot.Value;
            selection.enabled = Slot.owner == Game.Model.state.inventory && Slot.index == Game.Model.state.selected;
            icon.enabled = !s.Empty;
            if (!s.Empty)
            {
                var d = Game.Config.Item(s.id); icon.sprite = d.icon;
                ColorUtility.TryParseHtmlString("#" + d.color, out var color); icon.color = d.icon != null ? Color.white : color;
                itemName.text = d.name; count.text = s.count.ToString();
            }
            else { itemName.text = ""; count.text = ""; }
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (Game == null || (e.button != PointerEventData.InputButton.Left && e.button != PointerEventData.InputButton.Right)) return;
            Game.SlotDown(Slot, e.button == PointerEventData.InputButton.Right, Keyboard.current != null && Keyboard.current.shiftKey.isPressed, e.position);
        }
        public void OnPointerUp(PointerEventData e) { if (Game != null) Game.FinishSlotAction(e.position); }
        public void OnPointerEnter(PointerEventData e) { if (Game != null) Game.SetHovered(Slot); }
        public void OnPointerExit(PointerEventData e) { if (Game != null) Game.ClearHovered(Slot); }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { if (Game != null) Game.FinishSlotAction(e.position); }
        void OnDisable() { if (Game != null && Slot != null) Game.ClearHovered(Slot); }
    }
}
