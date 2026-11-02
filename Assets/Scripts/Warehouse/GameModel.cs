using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse
{
    [Serializable] public class NpcState
    {
        public string phase = "explore";
        public Vector2 position, waypoint;
        public bool moving, armed;
        public int rolls;
        public float elapsed, duration;
        public List<Stack> cargo = new List<Stack>();
    }
    [Serializable] public class Furniture
    {
        public string uid, id, name;
        public int x, y;
        public List<Stack> slots = new List<Stack>(), equipment = new List<Stack>();
        public NpcState npc;
        public float work;
        public bool processing;
        public List<Stack> reserved = new List<Stack>();
    }
    [Serializable] public class Drop
    {
        public Stack item; public Vector2 position;
    }
    [Serializable] public class SaveState
    {
        public int version = 1, width, height, selected, expansions;
        public Vector2 player;
        public List<Stack> inventory, equipment;
        public Stack cursor = new Stack();
        public List<Furniture> furniture = new List<Furniture>();
        public List<Drop> drops = new List<Drop>();
        public List<string> discovered = new List<string>(), unlocked = new List<string>();
        public float elapsed;
    }
    public class GameModel
    {
        public readonly GameConfig config;
        public SaveState state;
        public string message = "森の冒険者が最初の素材を持ち帰ります。";
        public Vector2 Entrance => new Vector2(state.width / 2, 0);
        public float WorkSpeed => state.equipment[1].Empty ? 1 : config.equipmentMultiplier;
        public GameModel(GameConfig config, SaveState loaded = null)
        {
            this.config = config;
            if (loaded != null) { state = loaded; NormalizeNpcOwnership(state, config); ReturnCursor(); Unlock(); return; }
            state = new SaveState { width = Math.Max(12, config.width), height = Math.Max(12, config.height), inventory = Inventory.Create(config.initialSlots), equipment = Inventory.Create(2) };
            int x = state.width / 2, y = state.height / 2;
            state.player = new Vector2(x, y - 3);
            Spawn("forest_delivery", x - 4, y);
            Spawn("chest_small", x, y);
            Spawn("workbench", x + 3, y);
            Unlock();
        }
        // JsonUtility can reconstruct an inline serializable NPC even when it was null.
        // Furniture definitions, rather than the presence of that object, own NPC behavior.
        public static void NormalizeNpcOwnership(SaveState state, GameConfig config)
        {
            foreach (var f in state.furniture)
            {
                if (config.Item(f.id).kind == "delivery" || f.npc == null) continue;
                if (f.npc.cargo != null) foreach (var item in f.npc.cargo) if (item != null && !item.Empty)
                    state.drops.Add(new Drop { item = item.Copy(), position = new Vector2(f.x, f.y) });
                f.npc = null;
            }
        }
        public bool HasNpc(Furniture f) => config.Item(f.id).kind == "delivery" && f.npc != null;
        public void Discover(string id) { if (string.IsNullOrEmpty(id) || state.discovered.Contains(id)) return; state.discovered.Add(id); message = "発見：" + config.Item(id).name; Unlock(); }
        void Unlock() { foreach (var r in config.recipes) if (!state.unlocked.Contains(r.output) && Array.TrueForAll(r.discoveries ?? Array.Empty<string>(), id => state.discovered.Contains(id))) state.unlocked.Add(r.output); }
        public void DropItem(string id, int count, Vector2 at) { if (count > 0) state.drops.Add(new Drop { item = new Stack(id, count), position = at }); }
        public void ReturnCursor() { if (state.cursor.Empty) return; var s = state.cursor; int left = Inventory.Add(state.inventory, s.id, s.count, config); DropItem(s.id, left, state.player); s.Clear(); }
        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < state.width && y < state.height;
        public Furniture At(int x, int y) => state.furniture.Find(f => { var d = config.Item(f.id); return x >= f.x && y >= f.y && x < f.x + d.width && y < f.y + d.height; });
        public bool Walkable(int x, int y) => Inside(x, y) && At(x, y) == null;
        public float Distance(Furniture f, Vector2 p)
        {
            var d = config.Item(f.id);
            return Vector2.Distance(p, new Vector2(Mathf.Clamp(p.x, f.x - .5f, f.x + d.width - .5f), Mathf.Clamp(p.y, f.y - .5f, f.y + d.height - .5f)));
        }
        public Furniture Nearest() { Furniture best = null; float dist = config.interactDistance; foreach (var f in state.furniture) { float d = Distance(f, state.player); if (d <= dist && config.Item(f.id).kind != "wall") { best = f; dist = d; } } return best; }
        bool CanStand(Vector2 p)
        {
            foreach (float dx in new[] { -.24f, .24f }) foreach (float dy in new[] { -.24f, .24f }) if (!Walkable(Mathf.FloorToInt(p.x + dx + .5f), Mathf.FloorToInt(p.y + dy + .5f))) return false;
            return true;
        }
        public void Move(Vector2 direction, float dt)
        {
            Vector2 delta = Vector2.ClampMagnitude(direction, 1) * config.moveSpeed * (state.equipment[0].Empty ? 1 : config.equipmentMultiplier) * dt;
            // Substeps prevent passing through a wall after a long frame.
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .15f)); delta /= steps;
            for (int i = 0; i < steps; i++) { var p = state.player; if (CanStand(p + new Vector2(delta.x, 0))) p.x += delta.x; if (CanStand(p + new Vector2(0, delta.y))) p.y += delta.y; state.player = p; }
        }
        public bool CanPlace(string id, int x, int y)
        {
            var d = config.Item(id); if (!d.Furniture) return false;
            for (int xx = x; xx < x + d.width; xx++) for (int yy = y; yy < y + d.height; yy++)
            {
                if (!Walkable(xx, yy) || new Vector2Int(xx, yy) == Vector2Int.RoundToInt(Entrance)) return false;
                if (Mathf.Abs(state.player.x - xx) < .75f && Mathf.Abs(state.player.y - yy) < .75f) return false;
                foreach (var f in state.furniture) if (HasNpc(f) && f.npc.phase != "explore" && Mathf.Abs(f.npc.position.x - xx) < .8f && Mathf.Abs(f.npc.position.y - yy) < .8f) return false;
            }
            return Vector2.Distance(state.player, new Vector2(x, y)) <= config.interactDistance + 1;
        }
        public bool Place(int x, int y)
        {
            Stack s = state.inventory[state.selected]; if (s.Empty || !CanPlace(s.id, x, y)) return false;
            Spawn(s.id, x, y); s.Take(1); return true;
        }
        public Furniture Spawn(string id, int x, int y)
        {
            var d = config.Item(id);
            var f = new Furniture { uid = Guid.NewGuid().ToString("N"), id = id, name = d.name, x = x, y = y, slots = Inventory.Create(d.slots), equipment = Inventory.Create(d.kind == "delivery" ? 3 : 0) };
            if (d.kind == "delivery") { f.npc = new NpcState { position = Entrance }; Snapshot(f); }
            state.furniture.Add(f); return f;
        }
        void Snapshot(Furniture f) { var n = f.npc; n.armed = !f.equipment[0].Empty; n.rolls = f.equipment[1].Empty ? config.normalRolls : config.bagRolls; n.duration = f.equipment[2].Empty ? config.expeditionSeconds : config.equippedExpeditionSeconds; }
        public void Dismantle(Furniture f)
        {
            var at = new Vector2(f.x, f.y);
            foreach (var list in new[] { f.slots, f.equipment, f.reserved }) foreach (var s in list) if (!s.Empty) DropItem(s.id, s.count, at);
            if (f.npc != null) foreach (var s in f.npc.cargo) if (!s.Empty) DropItem(s.id, s.count, at);
            DropItem(f.id, 1, at); state.furniture.Remove(f);
        }
        public int Available(string id) { int n = Inventory.Count(state.inventory, id); foreach (var f in state.furniture) if (config.Item(f.id).kind == "chest") n += Inventory.Count(f.slots, id); return n; }
        public bool Craft(RecipeDef recipe, Furniture station)
        {
            if (station == null || !state.furniture.Contains(station) || config.Item(station.id).kind != "workbench" || Distance(station, state.player) > config.interactDistance || !state.unlocked.Contains(recipe.output)) return false;
            foreach (var cost in recipe.ingredients) if (Available(cost.id) < cost.count) { message = "材料が足りません。通常チェストの素材も利用できます。"; return false; }
            var copy = Inventory.Copy(state.inventory);
            foreach (var cost in recipe.ingredients) Inventory.Remove(copy, cost.id, cost.count);
            if (Inventory.Add(copy, recipe.output, recipe.count, config) != 0) { message = "完成品を受け取る空きがありません。"; return false; }
            foreach (var cost in recipe.ingredients) { int left = Inventory.Remove(state.inventory, cost.id, cost.count); foreach (var f in state.furniture) if (config.Item(f.id).kind == "chest") left = Inventory.Remove(f.slots, cost.id, left); }
            Inventory.Add(state.inventory, recipe.output, recipe.count, config); Discover(recipe.output); message = config.Item(recipe.output).name + " を製作しました。"; return true;
        }
        public bool UseUpgrade(Stack slot)
        {
            if (slot.id != "inventory_upgrade" || slot.Empty) return false;
            if (state.inventory.Count >= config.maxSlots) { message = "所持枠は上限です。"; return false; }
            int n = Math.Min(config.upgradeSlots, config.maxSlots - state.inventory.Count); state.inventory.AddRange(Inventory.Create(n)); state.expansions++; slot.Take(1); message = "所持枠が " + state.inventory.Count + " 枠になりました。"; return true;
        }
        public void Tick(float dt)
        {
            state.elapsed += dt;
            foreach (var f in state.furniture) { if (HasNpc(f)) TickNpc(f, dt); if (config.Item(f.id).kind == "furnace") TickFurnace(f, dt); }
            for (int i = state.drops.Count - 1; i >= 0; i--) { var d = state.drops[i]; if (Vector2.Distance(d.position, state.player) > config.pickupDistance) continue; int old = d.item.count; d.item.count = Inventory.Add(state.inventory, d.item.id, old, config); if (d.item.count < old) Discover(d.item.id); if (d.item.count == 0) state.drops.RemoveAt(i); }
        }
        public void TickFurnace(Furniture f, float dt)
        {
            var output = f.slots[2]; bool room = output.Empty || (output.id == "iron_ingot" && output.count < config.Item("iron_ingot").max);
            if (!f.processing && room && state.discovered.Contains("iron_ore") && f.slots[0].id == "iron_ore" && f.slots[1].id == "wood" && !f.slots[0].Empty && !f.slots[1].Empty)
            {
                f.slots[0].Take(1); f.slots[1].Take(1); f.reserved = new List<Stack> { new Stack("iron_ore", 1), new Stack("wood", 1) }; f.processing = true; f.work = 0;
            }
            if (!f.processing) return;
            f.work = Mathf.Min(config.furnaceSeconds, f.work + dt * WorkSpeed);
            if (f.work >= config.furnaceSeconds && room) { output.id = "iron_ingot"; output.count++; f.processing = false; f.reserved.Clear(); f.work = 0; }
        }
        public void TickNpc(Furniture f, float dt)
        {
            if (!HasNpc(f)) return;
            var n = f.npc;
            if (n.phase == "explore")
            {
                n.elapsed += dt; if (n.elapsed < n.duration) return;
                n.cargo.Clear();
                for (int i = 0; i < n.rolls; i++) { float r = UnityEngine.Random.value; float w = n.armed ? config.armedWoodChance : config.woodChance; string id = r < w ? "wood" : r < w + config.stoneChance ? "stone" : r < w + config.stoneChance + config.oreChance ? "iron_ore" : "forest_crystal"; var s = n.cargo.Find(a => a.id == id); if (s == null) n.cargo.Add(new Stack(id, 1)); else s.count++; }
                n.phase = "return"; n.position = Entrance; n.moving = false;
            }
            if (n.phase == "return" && Distance(f, n.position) <= .6f)
            {
                foreach (var s in n.cargo) if (!s.Empty) { int left = Inventory.Add(f.slots, s.id, s.count, config); s.count = left; }
                if (n.cargo.Exists(s => !s.Empty)) return;
                n.cargo.Clear(); n.phase = "depart"; n.moving = false; Snapshot(f);
            }
            if (n.phase == "depart" && Vector2.Distance(n.position, Entrance) < .05f) { n.phase = "explore"; n.elapsed = 0; n.moving = false; return; }
            if (n.moving && !Walkable(Mathf.RoundToInt(n.waypoint.x), Mathf.RoundToInt(n.waypoint.y))) n.moving = false;
            if (!n.moving)
            {
                var next = NextStep(n.position, n.phase == "depart" ? (Func<int, int, bool>)((x, y) => new Vector2(x, y) == Entrance) : ((x, y) => Distance(f, new Vector2(x, y)) <= .6f));
                if (!next.HasValue) return; n.waypoint = next.Value; n.moving = true;
            }
            n.position = Vector2.MoveTowards(n.position, n.waypoint, config.moveSpeed * dt);
            if (Vector2.Distance(n.position, n.waypoint) < .01f) n.moving = false;
        }
        public Vector2? NextStep(Vector2 position, Func<int, int, bool> goal)
        {
            var start = Vector2Int.RoundToInt(position); var q = new Queue<Vector2Int>(); var prev = new Dictionary<Vector2Int, Vector2Int>(); q.Enqueue(start); prev[start] = start;
            var dirs = new[] { Vector2Int.up, Vector2Int.left, Vector2Int.right, Vector2Int.down };
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if (goal(p.x, p.y)) { while (prev[p] != start && prev[p] != p) p = prev[p]; return p; }
                foreach (var dir in dirs) { var t = p + dir; if (Walkable(t.x, t.y) && !prev.ContainsKey(t)) { prev[t] = p; q.Enqueue(t); } }
            }
            return null;
        }
        public bool Accepts(List<Stack> owner, int index, string id, Furniture opened)
        {
            if (owner == state.equipment) return config.Item(id).equipment == (index == 0 ? "player_boots" : "player_work");
            if (opened != null && owner == opened.equipment) return config.Item(id).equipment == new[] { "npc_weapon", "npc_bag", "npc_boots" }[index];
            if (opened != null && owner == opened.slots && config.Item(opened.id).kind == "furnace") return index == 0 ? id == "iron_ore" : index == 1 && id == "wood";
            return true;
        }
        public void Received(List<Stack> source, int index, string id, Furniture opened)
        {
            if (source == state.inventory || source == state.equipment || (opened != null && (source == opened.slots || source == opened.equipment))) Discover(id);
        }
    }
}
