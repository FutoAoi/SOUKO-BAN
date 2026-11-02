using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Database", fileName = "WarehouseDatabase")]
    public sealed class WarehouseDatabase : ScriptableObject
    {
        public ItemDefinition[] items;
        public GameConfig settings = new GameConfig();

        public GameConfig Build()
        {
            var result = JsonUtility.FromJson<GameConfig>(JsonUtility.ToJson(settings));
            var ids = new HashSet<string>();
            result.items = new ItemDef[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) throw new InvalidOperationException("データベースに未設定のアイテムがあります。");
                var d = items[i].Build();
                if (string.IsNullOrWhiteSpace(d.id) || !ids.Add(d.id) || d.max < 1 || (d.Furniture && (d.width < 1 || d.height < 1))) throw new InvalidOperationException("アイテムID・上限・占有サイズが不正です：" + items[i].name);
                result.items[i] = d;
            }
            foreach (var r in result.recipes)
            {
                if (!ids.Contains(r.output)) throw new InvalidOperationException("レシピの完成品IDがありません：" + r.output);
                foreach (var c in r.ingredients) if (!ids.Contains(c.id) || c.count < 1) throw new InvalidOperationException("レシピの材料が不正です：" + r.output);
                foreach (var id in r.discoveries ?? Array.Empty<string>()) if (!ids.Contains(id)) throw new InvalidOperationException("解放条件のIDがありません：" + id);
            }
            return result;
        }
    }
}
