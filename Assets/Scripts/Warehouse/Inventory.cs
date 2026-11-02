using System;
using System.Collections.Generic;

namespace Warehouse
{
    [Serializable] public class Stack
    {
        public string id = ""; public int count;
        public bool Empty => count <= 0 || string.IsNullOrEmpty(id);
        public Stack() { }
        public Stack(string id, int count) { this.id = id; this.count = count; }
        public Stack Copy() => Empty ? new Stack() : new Stack(id, count);
        public void Clear() { id = ""; count = 0; }
        public void Take(int amount) { count -= amount; if (count <= 0) Clear(); }
    }
    public static class Inventory
    {
        public static List<Stack> Create(int n) { var r = new List<Stack>(); for (int i = 0; i < n; i++) r.Add(new Stack()); return r; }
        public static List<Stack> Copy(List<Stack> src) => src.ConvertAll(s => s.Copy());
        public static int Count(List<Stack> slots, string id) { int n = 0; foreach (var s in slots) if (s.id == id && !s.Empty) n += s.count; return n; }
        public static int Add(List<Stack> slots, string id, int count, GameConfig config)
        {
            int max = config.Item(id).max;
            foreach (var s in slots) if (!s.Empty && s.id == id) { int n = Math.Min(max - s.count, count); s.count += n; count -= n; }
            foreach (var s in slots) if (s.Empty && count > 0) { int n = Math.Min(max, count); s.id = id; s.count = n; count -= n; }
            return count;
        }
        public static int Remove(List<Stack> slots, string id, int count)
        {
            foreach (var s in slots) if (s.id == id) { int n = Math.Min(s.count, count); s.Take(n); count -= n; } return count;
        }
        public static void Click(Stack slot, Stack cursor, bool right, int max, Func<string, bool> accepts)
        {
            if (cursor.Empty) { if (slot.Empty) return; int n = right ? (slot.count + 1) / 2 : slot.count; cursor.id = slot.id; cursor.count = n; slot.Take(n); return; }
            if (!accepts(cursor.id)) return;
            if (slot.Empty || slot.id == cursor.id) { int n = Math.Min(right ? 1 : cursor.count, max - (slot.Empty ? 0 : slot.count)); if (n <= 0) return; slot.id = cursor.id; slot.count += n; cursor.Take(n); }
            else if (!right) { var old = slot.Copy(); slot.id = cursor.id; slot.count = cursor.count; cursor.id = old.id; cursor.count = old.count; }
        }
        public static void Distribute(List<Stack> slots, Stack cursor, bool right, GameConfig config, Func<Stack, string, bool> accepts)
        {
            if (cursor.Empty || slots.Count == 0) return;
            string id = cursor.id; int max = config.Item(id).max;
            var targets = slots.FindAll(s => (s.Empty || s.id == id) && accepts(s, id) && s.count < max);
            if (targets.Count == 0) return;
            int share = right ? 1 : cursor.count / targets.Count;
            foreach (var s in targets) { int n = Math.Min(cursor.count, Math.Min(share, max - s.count)); if (n <= 0) continue; s.id = id; s.count += n; cursor.Take(n); }
        }
    }
}
