using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warehouse;

namespace WarehouseEditor
{
    // Local opt-in verification: place "validate" or "smoke" in Logs/Warehouse.request.
    [InitializeOnLoad]
    public static class WarehouseVerificationRunner
    {
        static double next;
        static WarehouseVerificationRunner()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) { SessionState.EraseString("Warehouse.TestSave"); SessionState.SetInt("Warehouse.SmokeStage", 0); }
            };
        }
        static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            const string request = "Logs/Warehouse.request";
            if (EditorApplication.isPlaying)
            {
                if (File.Exists(request) && File.ReadAllText(request).Trim() == "stop") { File.Delete(request); EditorApplication.isPlaying = false; return; }
                SmokeUpdate(); return;
            }
            if (!File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim(); File.Delete(request);
            try
            {
                if (command == "skin" || command == "skin-smoke") WarehouseKenneySkin.Apply();
                if (command == "world" || command == "world-smoke") WarehouseNinjaArt.Apply();
                if (command == "crafting" || command == "crafting-smoke") WarehouseCraftingContent.Apply();
                WarehouseSetup.Validate();
                File.WriteAllText("Logs/WarehouseResults.txt", "PASS: Unity compilation and model validation. " + DateTime.Now.ToString("O"));
                if (command == "smoke" || command == "skin-smoke" || command == "world-smoke" || command == "crafting-smoke")
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/Warehouse.unity");
                    SessionState.SetString("Warehouse.TestSave", System.IO.Path.Combine(Application.temporaryCachePath, "warehouse-smoke-" + Guid.NewGuid().ToString("N") + ".json"));
                    SessionState.SetInt("Warehouse.SmokeStage", 1);
                    EditorApplication.isPlaying = true;
                }
            }
            catch (Exception e) { File.WriteAllText("Logs/WarehouseResults.txt", "FAIL: " + e); Debug.LogException(e); }
        }
        static void SmokeUpdate()
        {
            int stage = SessionState.GetInt("Warehouse.SmokeStage", 0); if (stage == 0) return;
            var game = UnityEngine.Object.FindFirstObjectByType<WarehouseGame>(); if (game == null || game.Model == null) return;
            try
            {
                if (stage == 1)
                {
                    Application.runInBackground = true;
                    var g = game.Model;
                    // Real runtime scene, accelerated model time only for this isolated verification.
                    for (int i = 0; i < 600; i++) g.Tick(.1f);
                    var f = g.state.furniture.Find(a => a.npc != null);
                    if (!f.slots.Exists(s => !s.Empty)) throw new Exception("Runtime NPC did not deliver");
                    game.Save();
                    var loaded = SaveStore.Load(SaveStore.Path, g.config);
                    if (JsonUtility.ToJson(loaded) != JsonUtility.ToJson(g.state)) throw new Exception("Runtime save mismatch");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Play Mode initialization, 60s NPC delivery simulation, runtime save roundtrip.");
                    SessionState.SetInt("Warehouse.SmokeStage", 2);
                    next = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 2)
                {
                    if (game.ui == null || game.ui.canvas == null || UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null) throw new Exception("uGUI Canvas/Input System event module missing");
                    var delivery = game.Model.state.furniture.Find(f => f.npc != null);
                    game.Model.state.player = new Vector2(delivery.x, delivery.y - 1);
                    game.Interact();
                    SessionState.SetInt("Warehouse.SmokeStage", 3);
                    next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 3)
                {
                    if (!game.ui.inventoryPanel.activeInHierarchy || !game.ui.deliveryPanel.activeInHierarchy) throw new Exception("Delivery uGUI did not open");
                    var source = Array.Find(game.ui.GetComponentsInChildren<WarehouseSlotView>(), s => s.Slot.owner == game.Opened.slots && !s.Slot.Value.Empty);
                    var target = Array.Find(game.ui.GetComponentsInChildren<WarehouseSlotView>(), s => s.Slot.owner == game.Model.state.inventory && s.Slot.Value.Empty);
                    string id = source.Slot.Value.id; int count = source.Slot.Value.count;
                    var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, position = Vector2.zero };
                    source.OnPointerDown(pointer); source.OnPointerUp(pointer); target.OnPointerDown(pointer); target.OnPointerUp(pointer);
                    if (target.Slot.Value.id != id || target.Slot.Value.count != count || !game.Model.state.cursor.Empty || !source.Slot.Value.Empty) throw new Exception("uGUI pointer transfer failed");
                    var texture = new Texture2D(2, 2); var testIcon = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
                    var definition = game.Config.Item(id); var originalIcon = definition.icon;
                    try { definition.icon = testIcon; target.Refresh(); if (target.icon.sprite != testIcon || target.icon.color != Color.white) throw new Exception("Assigned item icon did not reach uGUI"); }
                    finally { definition.icon = originalIcon; target.Refresh(); UnityEngine.Object.Destroy(testIcon); UnityEngine.Object.Destroy(texture); }
                    game.TogglePause(); SessionState.SetFloat("Warehouse.PausedTime", game.Model.state.elapsed);
                    SessionState.SetInt("Warehouse.SmokeStage", 4); next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 4)
                {
                    if (game.Model.state.elapsed != SessionState.GetFloat("Warehouse.PausedTime", -1) || !game.ui.pausePanel.activeInHierarchy) throw new Exception("Pause did not stop runtime");
                    game.Resume(); ScreenCapture.CaptureScreenshot("Logs/WarehouseUGUI.png");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Serialized uGUI Canvas, InputSystem event module, delivery panel, pointer slot transfer, assigned icon rendering and paused game time. Screenshot requested.");
                    SessionState.SetInt("Warehouse.SmokeStage", 5); next = EditorApplication.timeSinceStartup + 1;
                }
                else if (stage == 5)
                {
                    game.CloseUi(); var bench = game.Model.state.furniture.Find(f => f.id == "workbench");
                    game.Model.state.player = new Vector2(bench.x, bench.y - 1); game.Interact();
                    SessionState.SetInt("Warehouse.SmokeStage", 6);
                }
                else if (stage == 6)
                {
                    if (!game.ui.recipesPanel.activeInHierarchy) throw new Exception("Workbench uGUI did not open");
                    game.Model.state.inventory[8] = new Stack("wood", 1);
                    var row = game.ui.recipeContent.GetChild(0).GetComponent<WarehouseRecipeView>(); row.Refresh();
                    var ingredient = Array.Find(row.GetComponentsInChildren<WarehouseIngredientView>(), v => v != row.ingredientTemplate);
                    if (ingredient == null || ingredient.icon.sprite != game.Config.Item("wood").icon || ingredient.quantity.text != game.Model.Available("wood") + " / 1" || row.materials.text != "" || row.title.fontSize < 20) throw new Exception("Readable recipe ingredient icons missing");
                    ScreenCapture.CaptureScreenshot("Logs/WarehouseRecipes.png");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Enlarged recipe text and ingredient icon/count display. Recipe screenshot requested.");
                    SessionState.SetInt("Warehouse.SmokeStage", 61); next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 61)
                {
                    int before = Inventory.Count(game.Model.state.inventory, "plank");
                    var row = game.ui.recipeContent.GetChild(0).GetComponent<WarehouseRecipeView>(); row.Refresh(); row.craft.onClick.Invoke();
                    if (Inventory.Count(game.Model.state.inventory, "plank") != before + 2) throw new Exception("Recipe button not connected");
                    game.CloseUi(); var chest = game.Model.state.furniture.Find(f => f.id == "chest_small"); game.Model.state.player = new Vector2(chest.x, chest.y - 1); game.Interact();
                    SessionState.SetInt("Warehouse.SmokeStage", 7);
                }
                else if (stage == 7)
                {
                    if (!game.ui.chestPanel.activeInHierarchy) throw new Exception("Chest uGUI did not open");
                    game.ui.chestName.text = "検証チェスト";
                    if (game.Opened.name != "検証チェスト") throw new Exception("Chest name input not connected");
                    game.CloseUi(); var furnace = game.Model.Spawn("furnace", 13, 9);
                    game.Model.state.player = new Vector2(13, 8); game.Interact();
                    if (game.Opened != furnace) throw new Exception("Furnace interaction failed");
                    SessionState.SetInt("Warehouse.SmokeStage", 8);
                }
                else if (stage == 8)
                {
                    if (!game.ui.furnacePanel.activeInHierarchy || game.ui.furnaceGrid.childCount != 3) throw new Exception("Furnace uGUI slots missing");
                    game.CloseUi(); game.ToggleInventory(); SessionState.SetInt("Warehouse.SmokeStage", 9);
                }
                else if (stage == 9)
                {
                    if (!game.ui.discoveryPanel.activeInHierarchy || !game.ui.inventoryPanel.activeInHierarchy) throw new Exception("Discovery uGUI did not open");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Workbench panel and recipe button, chest panel and name input, furnace slots, inventory/discovery panel.");
                    game.CloseUi();
                    if (game.worldArt != null)
                    {
                        var delivery = game.Model.state.furniture.Find(f => game.Model.HasNpc(f));
                        delivery.npc.phase = "depart"; delivery.npc.position = new Vector2(delivery.x, delivery.y - 1); delivery.npc.moving = false;
                        SessionState.SetInt("Warehouse.SmokeStage", 10); next = EditorApplication.timeSinceStartup + 1;
                    }
                    else SessionState.SetInt("Warehouse.SmokeStage", 0);
                }
                else if (stage == 10)
                {
                    var renderers = game.GetComponentsInChildren<SpriteRenderer>();
                    var player = Array.Find(renderers, r => r.name == "playersprite");
                    var npc = Array.Find(renderers, r => r.name.EndsWith("npcsprite"));
                    var floor = Array.Find(renderers, r => r.name == "tile0:0");
                    if (player == null || player.sprite == null || npc == null || npc.sprite == null || floor == null || floor.sprite != game.worldArt.floor) throw new Exception("World sprites not rendered");
                    ScreenCapture.CaptureScreenshot("Logs/WarehouseWorld.png");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Textured floor, player sprite and walking delivery NPC in the runtime scene. World screenshot requested.");
                    game.Model.state.inventory = Inventory.Create(Mathf.CeilToInt(game.Config.items.Length / 9f) * 9);
                    for (int i = 0; i < game.Config.items.Length; i++) game.Model.state.inventory[i] = new Stack(game.Config.items[i].id, 1);
                    game.Model.DropItem("iron_ore", 1, game.Model.state.player + new Vector2(2, 0));
                    game.ToggleInventory();
                    SessionState.SetInt("Warehouse.SmokeStage", 11); next = EditorApplication.timeSinceStartup + 1;
                }
                else if (stage == 11)
                {
                    foreach (var slot in game.ui.GetComponentsInChildren<WarehouseSlotView>())
                        if (!slot.Slot.Value.Empty && slot.Slot.owner == game.Model.state.inventory && (slot.icon.sprite != game.Config.Item(slot.Slot.Value.id).icon || slot.icon.color != Color.white)) throw new Exception("Inventory item art mismatch");
                    var drop = Array.Find(game.GetComponentsInChildren<SpriteRenderer>(), r => r.name.StartsWith("drop") && r.sprite == game.Config.Item("iron_ore").icon);
                    if (drop == null) throw new Exception("Dropped item icon missing");
                    ScreenCapture.CaptureScreenshot("Logs/WarehouseItems.png");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: All " + game.Config.items.Length + " inventory icons and dropped ore sprite rendered. Item screenshot requested.");
                    SessionState.SetInt("Warehouse.SmokeStage", 12); next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 12)
                {
                    if (!Array.Exists(game.Config.items, i => i.id == "reinforced_chest")) { EditorApplication.isPlaying = false; return; }
                    game.CloseUi(); var chest = game.Model.Spawn("reinforced_chest", 2, 4);
                    chest.slots[53] = new Stack("crystal_ingot", 7);
                    game.Model.state.player = new Vector2(2, 3); game.Interact();
                    SessionState.SetInt("Warehouse.SmokeStage", 13); next = EditorApplication.timeSinceStartup + 1;
                }
                else if (stage == 13)
                {
                    if (game.Opened == null || game.Opened.id != "reinforced_chest" || game.ui.storageGrid.childCount != 54) throw new Exception("Expanded chest UI slots missing");
                    var last = game.ui.storageGrid.GetChild(53).GetComponent<WarehouseSlotView>();
                    if (last.icon.sprite != game.Config.Item("crystal_ingot").icon || last.count.text != "7") throw new Exception("Expanded chest last slot not bound");
                    ScreenCapture.CaptureScreenshot("Logs/WarehouseExpandedChest.png");
                    File.AppendAllText("Logs/WarehouseResults.txt", "\nPASS: Reinforced chest displays all 54 slots, including the occupied last slot.");
                    SessionState.SetInt("Warehouse.SmokeStage", 14); next = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 14)
                {
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e) { SessionState.SetInt("Warehouse.SmokeStage", 0); File.AppendAllText("Logs/WarehouseResults.txt", "\nFAIL: " + e); Debug.LogException(e); }
        }
    }
}
