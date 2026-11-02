using System;
using System.IO;
using UnityEngine;

namespace Warehouse
{
    public static class SaveStore
    {
        public static string Path
        {
            get
            {
#if UNITY_EDITOR
                string test = UnityEditor.SessionState.GetString("Warehouse.TestSave", "");
                if (!string.IsNullOrEmpty(test)) return test;
#endif
                return System.IO.Path.Combine(Application.persistentDataPath, "warehouse-v1.json");
            }
        }
        public static SaveState Load(string path, GameConfig config)
        {
            var s = JsonUtility.FromJson<SaveState>(File.ReadAllText(path));
            if (s == null || s.version != 1 || s.width < 12 || s.height < 12 || s.width > 256 || s.height > 256 || s.inventory == null || s.inventory.Count < 9 || s.inventory.Count > config.maxSlots || s.equipment == null || s.equipment.Count != 2 || s.furniture == null || s.drops == null || s.cursor == null || s.discovered == null || s.unlocked == null || s.selected < 0 || s.selected > 8) throw new InvalidDataException("セーブ形式または基本状態が不正です。");
            void Check(Stack st) { if (st == null || st.count < 0 || (!st.Empty && st.count > config.Item(st.id).max)) throw new InvalidDataException("アイテムデータが不正です。"); }
            foreach (var stack in s.inventory) Check(stack); foreach (var stack in s.equipment) Check(stack); Check(s.cursor);
            var ids = new System.Collections.Generic.HashSet<string>();
            var cells = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (var f in s.furniture)
            {
                var d = config.Item(f.id);
                if (!d.Furniture || !ids.Add(f.uid) || f.slots == null || f.slots.Count != d.slots || f.equipment == null || f.equipment.Count != (d.kind == "delivery" ? 3 : 0) || f.reserved == null) throw new InvalidDataException("家具データが不正です。");
                for (int x = f.x; x < f.x + d.width; x++) for (int y = f.y; y < f.y + d.height; y++) if (x < 0 || y < 0 || x >= s.width || y >= s.height || !cells.Add(new Vector2Int(x, y))) throw new InvalidDataException("家具の配置が不正です。");
                foreach (var st in f.slots) Check(st); foreach (var st in f.equipment) Check(st); foreach (var st in f.reserved) Check(st);
                if (f.npc != null && f.npc.cargo != null) foreach (var st in f.npc.cargo) Check(st);
                if (d.kind == "delivery") { if (f.npc == null || f.npc.cargo == null || (f.npc.phase != "explore" && f.npc.phase != "return" && f.npc.phase != "depart")) throw new InvalidDataException("NPC状態が不正です。"); foreach (var st in f.npc.cargo) Check(st); }
            }
            GameModel.NormalizeNpcOwnership(s, config);
            foreach (var drop in s.drops) { if (drop == null) throw new InvalidDataException(); Check(drop.item); }
            return s;
        }
        public static void Save(SaveState state, string path = null)
        {
            path = path ?? Path; string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
    }
}
