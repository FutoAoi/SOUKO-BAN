using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseReadability
    {
        public static void Upgrade()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseAuthoring.UiPath);
            if (prefab == null || prefab.GetComponent<WarehouseUI>().authoringVersion >= 2) return;
            Edit("Assets/UI/Prefabs/RecipeRow.prefab", root =>
            {
                var row = root.GetComponent<WarehouseRecipeView>();
                root.GetComponent<LayoutElement>().preferredHeight = 112;
                root.GetComponent<LayoutElement>().minHeight = 112;
                ((RectTransform)root.transform).sizeDelta = new Vector2(520, 112);
                row.title.fontSize = 20; row.title.rectTransform.sizeDelta = new Vector2(345, 30);
                row.materials.fontSize = 18; row.materials.rectTransform.sizeDelta = new Vector2(345, 65);
                row.craft.GetComponentInChildren<Text>().fontSize = 20;
                row.craft.GetComponent<RectTransform>().anchoredPosition = new Vector2(410, -36);
                var content = Rect("Ingredients", root.transform, 57, 42, 345, 64);
                var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8; layout.childControlHeight = false; layout.childControlWidth = false;
                layout.childForceExpandHeight = false; layout.childForceExpandWidth = false;
                row.ingredientRoot = content;
                var template = Rect("IngredientTemplate", content, 0, 0, 104, 64);
                var view = template.gameObject.AddComponent<WarehouseIngredientView>();
                view.icon = Rect("Icon", template, 36, 0, 32, 32).gameObject.AddComponent<Image>();
                view.icon.preserveAspect = true; view.icon.raycastTarget = false;
                view.quantity = Rect("Quantity", template, 0, 34, 104, 28).gameObject.AddComponent<Text>();
                view.quantity.font = row.title.font; view.quantity.fontSize = 18; view.quantity.fontStyle = FontStyle.Bold;
                view.quantity.alignment = TextAnchor.MiddleCenter; view.quantity.raycastTarget = false;
                row.ingredientTemplate = view; template.gameObject.SetActive(false);
            });
            Edit("Assets/UI/Prefabs/InventorySlot.prefab", root =>
            {
                var slot = root.GetComponent<WarehouseSlotView>();
                slot.itemName.fontSize = 12; slot.itemName.rectTransform.sizeDelta = new Vector2(54, 24);
                slot.itemName.rectTransform.anchoredPosition = new Vector2(0, 14);
                slot.count.fontSize = 14; slot.count.rectTransform.sizeDelta = new Vector2(48, 18);
                slot.hint.fontSize = 12;
                slot.icon.rectTransform.sizeDelta = new Vector2(20, 20);
                slot.icon.rectTransform.anchoredPosition = new Vector2(0, -3);
            });
            Edit(WarehouseAuthoring.UiPath, root =>
            {
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    text.fontSize = Mathf.Max(text.fontSize, 18);
                    if (text.name == "CraftHint") text.fontSize = 16;
                }
                var ui = root.GetComponent<WarehouseUI>();
                ui.authoringVersion = 2;
            });
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        static void Edit(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
