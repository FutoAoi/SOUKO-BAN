using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Item", fileName = "NewItem")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("保存データとレシピは definition.id を参照します。既存IDは変更しないでください。")]
        public ItemDef definition = new ItemDef();
        [Tooltip("所持品・ホットバー・収納・レシピ・カーソルで表示するSprite。未設定なら色付きの仮アイコンを表示します。")]
        public Sprite icon;
        [Header("World appearance (furniture)")]
        public Sprite worldSprite;
        public Vector2 worldSize = Vector2.one;
        public Vector2 worldOffset;

        public ItemDef Build()
        {
            var copy = JsonUtility.FromJson<ItemDef>(JsonUtility.ToJson(definition));
            copy.icon = icon;
            copy.worldSprite = worldSprite; copy.worldSize = worldSize; copy.worldOffset = worldOffset;
            return copy;
        }
    }
}
