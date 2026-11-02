using UnityEngine;
using UnityEngine.UI;

namespace Warehouse
{
    public sealed class WarehouseIngredientView : MonoBehaviour
    {
        public Image icon;
        public Text quantity;
        public Color availableColor = new Color(.15f, .29f, .16f);
        public Color missingColor = new Color(.65f, .15f, .1f);

        public void Refresh(ItemDef item, int available, int required)
        {
            icon.sprite = item.icon;
            ColorUtility.TryParseHtmlString("#" + item.color, out var fallback);
            icon.color = item.icon != null ? Color.white : fallback;
            quantity.text = available + " / " + required;
            quantity.color = available >= required ? availableColor : missingColor;
        }
    }
}
