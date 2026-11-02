using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseSetup
    {
        static int assertions;
        [MenuItem("Warehouse/Open Game Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Warehouse.unity");
            WarehouseAuthoring.EnsureAssets(); WarehouseAuthoring.EnsureScene();
        }
        [MenuItem("Warehouse/Validate Prototype")]
        public static void Validate()
        {
            try
            {
                CreateScene();
                assertions = 0;
                var config = GameConfig.Load();
                Check(config.items.Length >= 18 && config.recipes.Length >= 13, "Base data loaded with optional content");
                TestAuthoring();
                TestWorldArt(config);
                TestExpandedContent(config);
                TestInventory(config); TestCraft(config); TestNpc(config); TestProcessing(config); TestPlacement(config); TestSave(config); TestNpcSerialization(config); TestProgression(config);
                Debug.Log("WAREHOUSE VALIDATION PASSED: " + assertions + " assertions");
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }
        public static void CreateScene()
        {
            WarehouseAuthoring.EnsureAssets();
            const string path = "Assets/Scenes/Warehouse.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera"; camera.GetComponent<Camera>().orthographic = true; camera.transform.position = new Vector3(12, 10, -10);
                new GameObject("Warehouse", typeof(WarehouseGame));
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            WarehouseAuthoring.EnsureScene();
            AssetDatabase.SaveAssets();
        }
        static void Check(bool condition, string name) { assertions++; if (!condition) throw new Exception("FAILED: " + name); }
        static void TestAuthoring()
        {
            var db = AssetDatabase.LoadAssetAtPath<WarehouseDatabase>(WarehouseAuthoring.DatabasePath);
            Check(db != null && db.items.Length >= 18, "ScriptableObject database includes base item assets");
            var copy = UnityEngine.Object.Instantiate(db.items[0]);
            var texture = new Texture2D(2, 2); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                copy.icon = sprite;
                var built = copy.Build(); Check(built.icon == sprite && built.id == copy.definition.id, "SO icon and stable ID passed to runtime");
                built.name = "runtime copy"; Check(copy.definition.name != built.name, "Runtime definitions do not mutate SO assets");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseAuthoring.UiPath).GetComponent<WarehouseUI>();
            Check(prefab != null && prefab.canvas != null && prefab.slotPrefab != null && prefab.recipePrefab != null, "Serialized Canvas and reusable view prefabs");
            Check(prefab.inventoryPanel != null && prefab.pausePanel != null && prefab.recoveryPanel != null && prefab.chestName != null && prefab.storageGrid != null && prefab.furnaceProgress != null, "Inventory, storage, processing and recovery references");
            Check(prefab.slotPrefab.icon != null && prefab.recipePrefab.icon != null && prefab.cursorIcon != null, "Icons supported in slots, recipes and cursor");
        }
        static int Total(IEnumerable<Stack> list) { int n = 0; foreach (var s in list) if (!s.Empty) n += s.count; return n; }
        static void TestExpandedContent(GameConfig config)
        {
            if (!Array.Exists(config.items, i => i.id == "stone_block")) return;
            var g = new GameModel(config); var bench = Kind(g, "workbench");
            foreach (var id in new[] { "plank", "stone", "iron_ingot", "forest_crystal" })
            { Inventory.Add(g.state.inventory, id, 90, config); g.Discover(id); }
            // Existing saves must unlock added recipes from their already discovered materials.
            g.state.unlocked.Remove("stone_block");
            var resumed = new GameModel(config, JsonUtility.FromJson<SaveState>(JsonUtility.ToJson(g.state)));
            resumed.state.player = new Vector2(bench.x, bench.y - 1);
            Check(resumed.state.unlocked.Contains("stone_block"), "Existing discoveries unlock newly added recipes on load");
            foreach (var pair in new[] { new[] { "timber_beam", "6" }, new[] { "stone_block", "2" }, new[] { "iron_plate", "4" }, new[] { "crystal_ingot", "1" }, new[] { "reinforced_chest", "1" }, new[] { "stone_wall", "1" } })
            {
                var recipe = Array.Find(config.recipes, r => r.output == pair[0]);
                Check(recipe != null, "Expansion recipe exists: " + pair[0]);
                for (int n = 0; n < int.Parse(pair[1]); n++) Check(resumed.Craft(recipe, resumed.state.furniture.Find(f => f.id == bench.id)), "Expansion recipe craft: " + pair[0]);
            }
            var chest = resumed.Spawn("reinforced_chest", 2, 2);
            Check(chest.slots.Count == 54 && config.Item("stone_wall").kind == "wall", "Expanded storage and blocking wall definitions");
            chest.slots[53] = new Stack("iron_plate", 2);
            var restored = JsonUtility.FromJson<SaveState>(JsonUtility.ToJson(resumed.state));
            Check(restored.furniture.Find(f => f.uid == chest.uid).slots[53].id == "iron_plate", "Expanded chest last slot survives serialization");
        }
        static void TestWorldArt(GameConfig config)
        {
            var art = Resources.Load<WarehouseWorldArt>("WarehouseWorldArt");
            if (art == null) return;
            foreach (var item in config.items) Check(item.icon != null, "Item icon assigned: " + item.id);
            Check(art.floor != null && art.entrance != null, "World floor and entrance sprites assigned");
            foreach (var character in new[] { art.player, art.npc }) for (int direction = 0; direction < 4; direction++)
            {
                for (int frame = 0; frame < 3; frame++) Check(character.Frame(direction, frame) != null, "Directional character idle/walk frame");
                Check(character.Frame(direction, 1) != character.Frame(direction, 2), "Walking frames are distinct sprites");
            }
            foreach (string id in new[] { "chest_small", "chest_large", "workbench", "forest_delivery", "furnace", "wall" })
            {
                var item = config.Item(id); Check(item.worldSprite != null && item.worldSize.x > 0 && item.worldSize.y > 0, "Furniture world art: " + id);
            }
        }
        static void TestInventory(GameConfig c)
        {
            var slots = Inventory.Create(3); var cursor = new Stack(); slots[0] = new Stack("wood", 99);
            Inventory.Click(slots[0], cursor, true, 99, id => true);
            Check(cursor.count == 50 && slots[0].count == 49, "Ceil half split");
            Inventory.Click(slots[1], cursor, true, 99, id => true);
            Check(cursor.count == 49 && slots[1].count == 1, "Place one");
            Inventory.Distribute(new List<Stack> { slots[0], slots[1], slots[2] }, cursor, false, c, (s, id) => true);
            Check(Total(slots) + cursor.count == 99 && cursor.count == 1, "Even drag keeps remainder");
            Inventory.Distribute(new List<Stack> { slots[0], slots[1] }, cursor, true, c, (s, id) => true);
            Check(Total(slots) == 99 && cursor.Empty, "Right drag preserves total");
            slots = Inventory.Create(1); slots[0] = new Stack("wood", 98);
            Check(Inventory.Add(slots, "wood", 5, c) == 4 && slots[0].count == 99, "Partial transfer at stack cap");
            cursor = new Stack("stone", 4); Inventory.Click(slots[0], cursor, false, 99, id => true);
            Check(slots[0].id == "stone" && cursor.id == "wood" && cursor.count == 99, "Swap different item");
            var equip = new Stack("player_boots", 1); cursor = new Stack("wood", 4); Inventory.Click(equip, cursor, false, 99, id => id == "player_boots");
            Check(equip.id == "player_boots" && cursor.count == 4, "Equipment rejects wrong item");
            var random = new System.Random(27); slots = Inventory.Create(12); Inventory.Add(slots, "wood", 700, c); cursor = new Stack();
            for (int i = 0; i < 3000; i++)
            {
                if (random.Next(3) == 0) Inventory.Distribute(slots.GetRange(0, random.Next(1, 12)), cursor, random.Next(2) == 0, c, (s, id) => true);
                else Inventory.Click(slots[random.Next(12)], cursor, random.Next(2) == 0, 99, id => true);
                Check(Total(slots) + cursor.count == 700 && slots.TrueForAll(s => s.count >= 0 && s.count <= 99), "Random inventory quantity conservation " + i);
            }
        }
        static Furniture Kind(GameModel g, string kind) => g.state.furniture.Find(f => g.config.Item(f.id).kind == kind);
        static void TestCraft(GameConfig c)
        {
            var g = new GameModel(c); var bench = Kind(g, "workbench"); g.state.player = new Vector2(bench.x, bench.y - 1);
            var plank = Array.Find(c.recipes, r => r.output == "plank");
            Kind(g, "delivery").slots[0] = new Stack("wood", 10);
            Check(!g.Craft(plank, bench) && g.Available("wood") == 0, "Delivery excluded from craft");
            var chest = Kind(g, "chest"); chest.slots[0] = new Stack("wood", 1);
            for (int i = 0; i < g.state.inventory.Count; i++) g.state.inventory[i] = new Stack("stone", 99);
            string before = JsonUtility.ToJson(g.state);
            Check(!g.Craft(plank, bench) && JsonUtility.ToJson(g.state) == before, "Full inventory craft atomic failure");
            g.state.inventory[0] = new Stack("wood", 1);
            Check(g.Craft(plank, bench) && g.state.inventory[0].id == "plank" && g.state.inventory[0].count == 2 && chest.slots[0].count == 1, "Craft uses freed material slot, player first");
            g.state.inventory = Inventory.Create(18); Check(g.Craft(plank, bench) && chest.slots[0].Empty, "Global chest ingredients consumed");
            Check(g.state.discovered.Contains("plank"), "Craft discovers output");
            g.state.inventory[0] = new Stack("inventory_upgrade", 3);
            Check(g.UseUpgrade(g.state.inventory[0]) && g.state.inventory.Count == 27, "Expand to 27");
            Check(g.UseUpgrade(g.state.inventory[0]) && g.state.inventory.Count == 36, "Expand to 36");
            Check(!g.UseUpgrade(g.state.inventory[0]) && g.state.inventory[0].count == 1, "Expansion cap preserves item");
        }
        static void TestNpc(GameConfig c)
        {
            var g = new GameModel(c); var f = Kind(g, "delivery"); var n = f.npc;
            Check(n.duration == 20 && n.rolls == 4, "Initial NPC snapshot");
            f.equipment[1] = new Stack("npc_bag", 1); f.equipment[2] = new Stack("npc_boots", 1);
            Check(n.duration == 20 && n.rolls == 4, "Equipment does not change current trip");
            g.TickNpc(f, 21); int generated = Total(n.cargo); Check(generated == 4 && n.phase == "return", "Generate cargo once");
            n.position = new Vector2(f.x, f.y - 1); n.moving = false;
            for (int i = 0; i < f.slots.Count; i++) f.slots[i] = new Stack("wood", 99);
            n.cargo = new List<Stack> { new Stack("wood", 3), new Stack("stone", 2) }; f.slots[0].count = 98;
            g.TickNpc(f, .1f); Check(Total(n.cargo) == 4 && f.slots[0].count == 99, "Partial deposit retains cargo");
            for (int i = 0; i < 100; i++) g.TickNpc(f, .1f);
            Check(Total(n.cargo) == 4, "Waiting does not reroll cargo");
            f.slots[1].Clear(); f.slots[2].Clear(); g.TickNpc(f, .1f);
            Check(n.phase == "depart" && Total(n.cargo) == 0 && n.rolls == 6 && n.duration == 16, "Empty space resumes delivery, next departure snapshot");
            Check(g.state.discovered.Count == 0, "Deposit does not discover");
            n.phase = "return"; n.position = g.Entrance; n.moving = false; n.cargo = new List<Stack> { new Stack("wood", 1) };
            var walls = new List<Furniture>();
            foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up }) walls.Add(g.Spawn("wall", (int)g.Entrance.x + d.x, d.y));
            Vector2 at = n.position; g.TickNpc(f, .1f); Check(n.position == at, "Blocked path waits");
            g.Dismantle(walls[2]); g.TickNpc(f, .1f); Check(n.position != at, "Restored path resumes");
            for (int i = 0; i < f.slots.Count; i++) f.slots[i].Clear(); n.cargo = new List<Stack> { new Stack("stone", 5) };
            int drops = 0; foreach (var d in g.state.drops) drops += d.item.count;
            g.Dismantle(f); int after = 0; foreach (var d in g.state.drops) after += d.item.count;
            Check(after - drops == 8 && !g.state.furniture.Contains(f), "Dismantle drops NPC cargo, equipment, furniture");
        }
        static void TestProcessing(GameConfig c)
        {
            var g = new GameModel(c); var f = g.Spawn("furnace", 2, 2); f.slots[0] = new Stack("iron_ore", 2); f.slots[1] = new Stack("wood", 2);
            g.TickFurnace(f, 1); Check(!f.processing, "Smelting locked before ore discovery");
            g.Discover("iron_ore"); g.TickFurnace(f, 3);
            Check(f.processing && f.work == 3 && f.reserved.Count == 2 && f.slots[0].count == 1, "Inputs reserved once");
            g.state.equipment[1] = new Stack("work_gloves", 1); g.TickFurnace(f, 4);
            Check(!f.processing && f.slots[2].count == 1 && !g.state.discovered.Contains("iron_ingot"), "Work multiplier, no discovery before collection");
            f.slots[2].count = 99; g.TickFurnace(f, 10); Check(!f.processing && f.slots[0].count == 1, "Full output does not consume inputs");
            f.slots[2].Clear(); g.TickFurnace(f, 1); f.slots[2] = new Stack("iron_ingot", 99); g.TickFurnace(f, 20);
            Check(f.processing && f.work == 8 && f.reserved.Count == 2, "Completed output blocked retains work");
            f.slots[2].Take(1); g.TickFurnace(f, .1f); Check(!f.processing && f.slots[2].count == 99, "Output space resumes completion");
        }
        static void TestPlacement(GameConfig c)
        {
            var g = new GameModel(c); var chest = Kind(g, "chest"); g.state.inventory[0] = new Stack("chest_large", 1);
            Check(!g.Place(chest.x, chest.y) && g.state.inventory[0].count == 1, "Invalid placement preserves item");
            int x = (int)g.state.player.x - 2, y = (int)g.state.player.y;
            Check(g.Place(x, y) && g.At(x, y) == g.At(x + 1, y), "Multi cell placement occupancy");
            var f = g.At(x, y); f.slots[0] = new Stack("stone", 12); g.Dismantle(f);
            Check(g.state.drops.Count == 2 && g.state.drops[0].item.count == 12 && g.state.drops[1].item.id == "chest_large", "Contents plus furniture, no double refund");
            g.state.inventory = Inventory.Create(18); for (int i = 0; i < 18; i++) g.state.inventory[i] = new Stack("wood", 99);
            g.state.player = g.state.drops[0].position; g.Tick(.1f); Check(g.state.drops.Count == 2, "Full inventory leaves ground drops");
        }
        static void TestSave(GameConfig c)
        {
            var g = new GameModel(c); g.state.cursor = new Stack("wood", 42); g.state.elapsed = 127;
            var f = g.Spawn("furnace", 3, 3); f.processing = true; f.work = 4.3f; f.reserved.Add(new Stack("wood", 1)); f.reserved.Add(new Stack("iron_ore", 1));
            var n = Kind(g, "delivery").npc; n.phase = "return"; n.position = new Vector2(9, 4); n.cargo.Add(new Stack("forest_crystal", 2));
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "warehouse-validation.json");
            SaveStore.Save(g.state, path); var loaded = SaveStore.Load(path, c);
            Check(JsonUtility.ToJson(g.state) == JsonUtility.ToJson(loaded), "Save roundtrip including cursor, reserved inputs, cargo");
            SaveStore.Save(g.state, path); Check(File.Exists(path + ".bak"), "Backup on replacement");
            var restored = new GameModel(c, loaded); Check(restored.state.cursor.Empty && Inventory.Count(restored.state.inventory, "wood") == 42 && restored.state.elapsed == 127, "Cursor returned once, no offline time");
            File.WriteAllText(path, "{broken"); bool rejected = false; try { SaveStore.Load(path, c); } catch { rejected = true; }
            Check(rejected && File.ReadAllText(path) == "{broken", "Corrupt save rejected without overwrite");
            File.Delete(path); File.Delete(path + ".bak");
        }
        static void TestNpcSerialization(GameConfig c)
        {
            var g = new GameModel(c);
            // Reproduce the reported save: non-delivery furniture has an inline NPC,
            // but correctly has zero equipment slots. Returning previously called Snapshot.
            foreach (var f in g.state.furniture) if (c.Item(f.id).kind != "delivery")
                f.npc = new NpcState { phase = "return", position = new Vector2(f.x, f.y - 1) };
            g.Tick(.1f);
            Check(Kind(g, "chest").npc.phase == "return", "Non-delivery inline NPC is never ticked");
            var loaded = JsonUtility.FromJson<SaveState>(JsonUtility.ToJson(g.state));
            g = new GameModel(c, loaded);
            Check(g.state.furniture.TrueForAll(f => c.Item(f.id).kind == "delivery" || f.npc == null), "Deserialize removes phantom NPCs from ordinary furniture");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < 1200; i++) g.Tick(.1f);
                g = new GameModel(c, JsonUtility.FromJson<SaveState>(JsonUtility.ToJson(g.state)));
                Check(g.state.furniture.FindAll(f => g.HasNpc(f)).Count == 1 && Kind(g, "chest").equipment.Count == 0, "Repeated save/resume keeps a single real NPC " + cycle);
            }
            Check(Total(Kind(g, "delivery").slots) > 0, "Real NPC continues to deliver after repeated resumes");
            var chest = Kind(g, "chest"); chest.npc = new NpcState(); chest.npc.cargo.Add(new Stack("stone", 3));
            GameModel.NormalizeNpcOwnership(g.state, c); int drops = g.state.drops.Count;
            Check(chest.npc == null && drops == 1 && g.state.drops[0].item.id == "stone" && g.state.drops[0].item.count == 3, "Legacy non-delivery cargo retained as a ground item");
            GameModel.NormalizeNpcOwnership(g.state, c); Check(g.state.drops.Count == drops, "Repeated normalization does not duplicate legacy cargo");
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "warehouse-npc-regression.json");
            try
            {
                chest.npc = new NpcState { phase = "depart" };
                SaveStore.Save(g.state, path); var bytes = File.ReadAllText(path);
                var restored = SaveStore.Load(path, c);
                Check(restored.furniture.TrueForAll(f => c.Item(f.id).kind == "delivery" || f.npc == null), "File load normalizes phantom NPCs");
                Check(File.ReadAllText(path) == bytes, "Load normalization does not overwrite the source save");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
            // Exercise the user's actual existing save without modifying it.
            string existing = System.IO.Path.Combine(Application.persistentDataPath, "warehouse-v1.json");
            if (File.Exists(existing))
            {
                string bytes = File.ReadAllText(existing);
                var resumed = new GameModel(c, SaveStore.Load(existing, c));
                for (int i = 0; i < 1200; i++) resumed.Tick(.1f);
                Check(resumed.state.furniture.TrueForAll(f => c.Item(f.id).kind == "delivery" || f.npc == null), "Existing user save resumes for 120 seconds without phantom NPCs");
                Check(File.ReadAllText(existing) == bytes, "Existing user save left intact by regression test");
            }
        }
        static void TestProgression(GameConfig c)
        {
            var previousRandom = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(8273);
                var g = new GameModel(c); var delivery = Kind(g, "delivery"); var chest = Kind(g, "chest"); var bench = Kind(g, "workbench");
                Check(Total(g.state.inventory) == 0 && Total(chest.slots) == 0 && Total(delivery.slots) == 0, "Progression begins with no materials");
                for (int frame = 0; frame < 20000 && (g.Available("wood") < 55 || g.Available("stone") < 40 || g.Available("iron_ore") < 14); frame++)
                {
                    g.Tick(.2f);
                    // Simulate explicit collection and storage. No materials are granted by the test.
                    foreach (var s in delivery.slots) if (!s.Empty)
                    {
                        string id = s.id; int before = s.count; s.count = Inventory.Add(g.state.inventory, id, s.count, c);
                        if (s.count < before) g.Discover(id); if (s.count == 0) s.Clear();
                    }
                    foreach (var s in g.state.inventory) if (!s.Empty) { s.count = Inventory.Add(chest.slots, s.id, s.count, c); if (s.count == 0) s.Clear(); }
                }
                Check(g.Available("wood") >= 55 && g.Available("stone") >= 40 && g.Available("iron_ore") >= 14, "Random deliveries supply full progression");
                g.state.player = new Vector2(bench.x, bench.y - 1);
                var plank = Array.Find(c.recipes, r => r.output == "plank");
                for (int i = 0; i < 40; i++) Check(g.Craft(plank, bench), "Progression crafts planks " + i);
                Check(g.Craft(Array.Find(c.recipes, r => r.output == "furnace"), bench), "Progression crafts furnace");
                int furnaceIndex = g.state.inventory.FindIndex(s => s.id == "furnace");
                g.state.selected = furnaceIndex; g.state.player = new Vector2(14, 9);
                Check(g.Place(13, 10), "Progression places crafted furnace");
                var furnace = Kind(g, "furnace");
                int oreLeft = Inventory.Remove(g.state.inventory, "iron_ore", 14); oreLeft = Inventory.Remove(chest.slots, "iron_ore", oreLeft);
                int fuelLeft = Inventory.Remove(g.state.inventory, "wood", 14); fuelLeft = Inventory.Remove(chest.slots, "wood", fuelLeft);
                Check(oreLeft == 0 && fuelLeft == 0, "Progression manually transfers real inputs");
                furnace.slots[0] = new Stack("iron_ore", 14); furnace.slots[1] = new Stack("wood", 14);
                for (int frame = 0; frame < 650; frame++) g.Tick(.2f);
                Check(furnace.slots[2].count == 14 && !g.state.discovered.Contains("iron_ingot"), "Progression produces fourteen ingots");
                int left = Inventory.Add(g.state.inventory, "iron_ingot", furnace.slots[2].count, c); Check(left == 0, "Progression collects output"); furnace.slots[2].Clear(); g.Discover("iron_ingot");
                g.state.player = new Vector2(bench.x, bench.y - 1);
                foreach (string id in new[] { "chest_large", "npc_sword", "forest_delivery" }) Check(g.Craft(Array.Find(c.recipes, r => r.output == id), bench), "Progression crafts " + id);
                var sword = g.state.inventory.Find(s => s.id == "npc_sword"); delivery.equipment[0] = sword.Copy(); sword.Clear();
                g.state.selected = g.state.inventory.FindIndex(s => s.id == "forest_delivery"); g.state.player = new Vector2(15, 6);
                Check(g.Place(15, 8), "Progression adds a second delivery box");
                var second = g.At(15, 8);
                for (int frame = 0; frame < 400; frame++) g.Tick(.2f);
                Check(Total(second.slots) > 0 && delivery.npc.armed && g.state.furniture.FindAll(f => f.npc != null).Count == 2, "Two NPCs deliver independently with updated equipment");
                var loaded = JsonUtility.FromJson<SaveState>(JsonUtility.ToJson(g.state)); var continued = new GameModel(c, loaded); float elapsed = continued.state.elapsed;
                continued.Tick(.2f); Check(continued.state.elapsed > elapsed && continued.state.furniture.Count == g.state.furniture.Count, "Progression resumes from serialized world");
            }
            finally { UnityEngine.Random.state = previousRandom; }
        }
    }
}
