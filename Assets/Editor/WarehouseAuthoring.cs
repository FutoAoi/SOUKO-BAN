using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseAuthoring
    {
        public const string DatabasePath = "Assets/Resources/WarehouseDatabase.asset";
        public const string UiPath = "Assets/UI/Prefabs/WarehouseUI.prefab";
        const string SlotPath = "Assets/UI/Prefabs/InventorySlot.prefab";
        const string RecipePath = "Assets/UI/Prefabs/RecipeRow.prefab";
        static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        [MenuItem("Warehouse/Prepare Editable Assets")]
        public static void Prepare()
        {
            EnsureAssets();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Warehouse.unity"); EnsureScene();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(UiPath);
        }
        public static void EnsureAssets()
        {
            Directory.CreateDirectory("Assets/Data/Items"); Directory.CreateDirectory("Assets/UI/Prefabs");
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<WarehouseDatabase>(DatabasePath) == null)
            {
                var legacy = JsonUtility.FromJson<GameConfig>(File.ReadAllText("Assets/Resources/WarehouseConfig.json"));
                var db = ScriptableObject.CreateInstance<WarehouseDatabase>();
                db.settings = legacy; db.items = new ItemDefinition[legacy.items.Length];
                for (int i = 0; i < legacy.items.Length; i++)
                {
                    string path = "Assets/Data/Items/" + legacy.items[i].id + ".asset";
                    var asset = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (asset == null) { asset = ScriptableObject.CreateInstance<ItemDefinition>(); asset.definition = legacy.items[i]; AssetDatabase.CreateAsset(asset, path); }
                    db.items[i] = asset;
                }
                db.settings.items = null;
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            var slot = AssetDatabase.LoadAssetAtPath<WarehouseSlotView>(SlotPath);
            if (slot == null) slot = CreateSlot();
            var recipe = AssetDatabase.LoadAssetAtPath<WarehouseRecipeView>(RecipePath);
            if (recipe == null) recipe = CreateRecipe();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UiPath) == null) CreateUI(slot, recipe);
            UpgradeInitialLayout();
            WarehouseReadability.Upgrade();
            AssetDatabase.SaveAssets();
        }
        // One-time correction for the first generated prefab. Subsequent user edits are retained.
        static void UpgradeInitialLayout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPath);
            if (prefab.GetComponent<WarehouseUI>().authoringVersion >= 1) return;
            var slot = PrefabUtility.LoadPrefabContents(SlotPath);
            var inset = (RectTransform)slot.transform.Find("Selection/Inset"); Stretch(inset); inset.offsetMin = new Vector2(2, 2); inset.offsetMax = new Vector2(-2, -2);
            PrefabUtility.SaveAsPrefabAsset(slot, SlotPath); PrefabUtility.UnloadPrefabContents(slot);
            var root = PrefabUtility.LoadPrefabContents(UiPath); var ui = root.GetComponent<WarehouseUI>();
            foreach (string path in new[] { "Gameplay/Header", "Gameplay/Hotbar" })
            {
                var parent = root.transform.Find(path); var children = new System.Collections.Generic.List<RectTransform>(); foreach (Transform child in parent) children.Add((RectTransform)child);
                var content = Rect("Content", parent, 0, 0, 1280, path.EndsWith("Header") ? 96 : 112);
                content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
                foreach (var child in children) { Vector2 position = child.anchoredPosition; child.SetParent(content, false); child.anchoredPosition = position; }
            }
            var panel = (RectTransform)ui.inventoryPanel.transform; panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1); panel.pivot = new Vector2(.5f, 1); panel.anchoredPosition = new Vector2(0, -108);
            var menu = (RectTransform)ui.pausePanel.transform.Find("Menu"); menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(.5f, .5f); menu.anchoredPosition = Vector2.zero;
            var help = (RectTransform)ui.helpPanel.transform; help.anchorMin = help.anchorMax = new Vector2(1, 1); help.pivot = new Vector2(1, 1); help.anchoredPosition = new Vector2(-25, -110);
            ui.authoringVersion = 1; PrefabUtility.SaveAsPrefabAsset(root, UiPath); PrefabUtility.UnloadPrefabContents(root);
        }
        public static void EnsureScene()
        {
            const string scenePath = "Assets/Scenes/Warehouse.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool temporary = !scene.IsValid() || !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            WarehouseGame game = null; WarehouseUI ui = null; EventSystem events = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (game == null) game = root.GetComponentInChildren<WarehouseGame>(true);
                if (ui == null) ui = root.GetComponentInChildren<WarehouseUI>(true);
                if (events == null) events = root.GetComponentInChildren<EventSystem>(true);
            }
            if (game == null) return;
            bool changed = false;
            if (ui == null) { ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPath), scene)).GetComponent<WarehouseUI>(); changed = true; }
            if (game.ui != ui || ui.game != game) { game.ui = ui; ui.game = game; EditorUtility.SetDirty(game); EditorUtility.SetDirty(ui); PrefabUtility.RecordPrefabInstancePropertyModifications(ui); changed = true; }
            if (events == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventObject, scene); changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(scene);
            if (temporary) EditorSceneManager.CloseScene(scene, true);
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }
        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero;
        }
        static Image Panel(string name, Transform parent, float x, float y, float w, float h, string color = "182D32")
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = C(color); return image;
        }
        static Text Text(string name, Transform parent, float x, float y, float w, float h, string value, int size = 16)
        {
            var r = Rect(name, parent, x, y, w, h); var t = r.gameObject.AddComponent<Text>(); t.font = Font; t.fontSize = size; t.color = C("E7EADB"); t.text = value;
            t.raycastTarget = false; t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        static Button Button(string name, Transform parent, float x, float y, float w, float h, string caption)
        {
            var image = Panel(name, parent, x, y, w, h, "3B5759"); var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Text("Label", b.transform, 8, 0, w - 16, h, caption); label.alignment = TextAnchor.MiddleCenter; return b;
        }
        static Transform Grid(string name, Transform parent, float x, float y, float w, float h, int columns, Vector2 cell)
        {
            var r = Rect(name, parent, x, y, w, h); var layout = r.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = columns; layout.cellSize = cell; layout.spacing = new Vector2(5, 6); return r;
        }
        static Image Progress(string name, Transform parent, float x, float y, float w, float h)
        {
            var bg = Panel(name, parent, x, y, w, h, "405655");
            var bar = Panel("Fill", bg.transform, 0, 0, w, h, "DEBB79");
            // UI Image.fillAmount requires a sprite, including for a plain colored bar.
            bar.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillOrigin = 0; bar.fillAmount = 0; bar.raycastTarget = false; return bar;
        }
        static ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h, out RectTransform content)
        {
            var root = Rect(name, parent, x, y, w, h); var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", root, 0, 0, w, h, "20383D"); Stretch((RectTransform)viewport.transform); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport.transform, 0, 0, w - 10, h);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.sizeDelta = new Vector2(-10, h);
            scroll.viewport = (RectTransform)viewport.transform; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30; return scroll;
        }
        static WarehouseSlotView CreateSlot()
        {
            var root = Panel("InventorySlot", null, 0, 0, 58, 66, "2D4549"); var slot = root.gameObject.AddComponent<WarehouseSlotView>();
            var border = Panel("Selection", root.transform, 0, 0, 58, 66, "D5B677"); Stretch((RectTransform)border.transform); border.raycastTarget = false;
            var inside = Panel("Inset", border.transform, 2, 2, 54, 62, "2D4549"); inside.raycastTarget = false;
            var ir = (RectTransform)inside.transform; Stretch(ir); ir.offsetMin = new Vector2(2, 2); ir.offsetMax = new Vector2(-2, -2);
            slot.selection = border;
            slot.icon = Panel("Icon", root.transform, 18, 8, 26, 26, "DEB878"); slot.icon.preserveAspect = true; slot.icon.raycastTarget = false;
            slot.hint = Text("Hint", root.transform, 4, 0, 51, 15, "", 10);
            slot.itemName = Text("ItemName", root.transform, 3, 34, 52, 20, "", 10); slot.itemName.alignment = TextAnchor.MiddleCenter;
            slot.count = Text("Count", root.transform, 5, 50, 48, 15, "", 11); slot.count.alignment = TextAnchor.MiddleRight;
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, SlotPath).GetComponent<WarehouseSlotView>(); UnityEngine.Object.DestroyImmediate(root.gameObject); return saved;
        }
        static WarehouseRecipeView CreateRecipe()
        {
            var root = Panel("RecipeRow", null, 0, 0, 520, 86, "2B4448"); var row = root.gameObject.AddComponent<WarehouseRecipeView>();
            var size = root.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = 86; size.minHeight = 86;
            row.icon = Panel("Icon", root.transform, 10, 18, 36, 36, "DEB878"); row.icon.preserveAspect = true; row.icon.raycastTarget = false;
            row.title = Text("Title", root.transform, 57, 6, 335, 28, "完成品", 16);
            row.materials = Text("Materials", root.transform, 57, 35, 335, 44, "材料", 13);
            row.craft = Button("Craft", root.transform, 410, 20, 96, 44, "製作");
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RecipePath).GetComponent<WarehouseRecipeView>(); UnityEngine.Object.DestroyImmediate(root.gameObject); return saved;
        }
        static void CreateUI(WarehouseSlotView slot, WarehouseRecipeView recipe)
        {
            var root = new GameObject("WarehouseUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WarehouseUI));
            var ui = root.GetComponent<WarehouseUI>(); ui.canvas = root.GetComponent<Canvas>(); ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay; ui.canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui.slotPrefab = slot; ui.recipePrefab = recipe;
            var gameplay = Rect("Gameplay", root.transform, 0, 0, 1280, 800); Stretch(gameplay); ui.gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            ui.worldLabelRoot = Rect("WorldLabels", gameplay, 0, 0, 1280, 800); Stretch(ui.worldLabelRoot);
            var worldLabel = Text("FurnitureLabel", null, 0, 0, 220, 60, "家具名", 13); worldLabel.alignment = TextAnchor.LowerCenter;
            worldLabel.rectTransform.anchorMin = worldLabel.rectTransform.anchorMax = new Vector2(.5f, .5f); worldLabel.rectTransform.pivot = new Vector2(.5f, 0);
            ui.worldLabelPrefab = PrefabUtility.SaveAsPrefabAsset(worldLabel.gameObject, "Assets/UI/Prefabs/FurnitureLabel.prefab").GetComponent<Text>(); UnityEngine.Object.DestroyImmediate(worldLabel.gameObject);
            var header = Panel("Header", gameplay, 0, 0, 1280, 96); header.rectTransform.anchorMax = Vector2.one; header.rectTransform.anchorMin = new Vector2(0, 1); header.rectTransform.sizeDelta = new Vector2(0, 96);
            Text("Title", header.transform, 24, 12, 500, 34, "森の倉庫 / FOREST DEPOT", 25);
            ui.messageLabel = Text("Message", header.transform, 24, 54, 790, 29, "森の冒険者を待ちましょう", 14);
            ui.saveLabel = Text("SaveStatus", header.transform, 900, 10, 350, 25, "未保存", 13);
            ui.helpButton = Button("Help", header.transform, 915, 46, 150, 34, "操作ガイド");
            ui.pauseButton = Button("Pause", header.transform, 1080, 46, 170, 34, "停止 / Esc");
            var help = Panel("HelpPanel", gameplay, 970, 110, 285, 220); ui.helpPanel = help.gameObject;
            Text("Guide", help.transform, 15, 10, 255, 200, "WASD 移動 / E 設備\nTab 所持品 / 1〜9 選択\n家具を選択 → 左クリック設置\n右長押し 家具を解体\nEsc 一時停止・保存\n\n納品物を取り出すと発見。\nチェストの素材で製作できます。", 14);
            var hotbar = Panel("Hotbar", gameplay, 0, 688, 1280, 112); hotbar.rectTransform.anchorMin = Vector2.zero; hotbar.rectTransform.anchorMax = new Vector2(1, 0); hotbar.rectTransform.pivot = Vector2.zero; hotbar.rectTransform.anchoredPosition = Vector2.zero; hotbar.rectTransform.sizeDelta = new Vector2(0, 112);
            ui.capacityLabel = Text("Capacity", hotbar.transform, 24, 15, 180, 25, "所持品 18枠");
            ui.inventoryButton = Button("Inventory", hotbar.transform, 24, 51, 175, 38, "所持品 / Tab");
            ui.hotbarGrid = Grid("Slots", hotbar.transform, 225, 19, 710, 72, 9, new Vector2(73, 70));
            ui.selectedLabel = Text("SelectedItem", hotbar.transform, 950, 15, 300, 30, "家具を選択", 13);
            ui.interactButton = Button("Interact", hotbar.transform, 960, 51, 290, 38, "近くの設備を開く / E");
            ui.promptLabel = Text("InteractionPrompt", gameplay, 410, 637, 460, 34, "", 17);
            ui.dismantleProgress = Progress("DismantleProgress", gameplay, 490, 615, 300, 14);
            var inventory = Panel("InventoryPanel", gameplay, 35, 108, 1210, 566); ui.inventoryPanel = inventory.gameObject;
            ui.titleLabel = Text("Title", inventory.transform, 22, 10, 850, 40, "所持品", 25);
            ui.closeButton = Button("Close", inventory.transform, 1100, 13, 86, 34, "閉じる");
            Text("Controls", inventory.transform, 23, 60, 560, 46, "左：持つ・置く / 右：半分・1個 / Shift：収納間移動\n持ったままドラッグで分配。スロット上の1〜9で交換。", 13);
            ui.inventoryGrid = Grid("InventorySlots", inventory.transform, 23, 115, 565, 198, 9, new Vector2(57, 60));
            Text("EquipmentTitle", inventory.transform, 23, 322, 560, 26, "プレイヤー装備", 14);
            ui.playerEquipmentGrid = Grid("PlayerEquipment", inventory.transform, 23, 354, 210, 76, 2, new Vector2(92, 70));
            ui.upgradeButton = Button("UseUpgrade", inventory.transform, 245, 368, 205, 42, "所持枠拡張を使用");
            Text("CraftHint", inventory.transform, 23, 455, 560, 80, "作業台は所持品と全通常チェストの素材を使用。\n納品ボックス・かまど・装備中の品は材料の対象外。\n収納画面中も時間が進みます。Escで停止。", 13);
            var right = Rect("Station", inventory.transform, 620, 65, 565, 475);
            var chest = Rect("ChestPanel", right, 0, 0, 550, 70); ui.chestPanel = chest.gameObject;
            Text("NameTitle", chest, 0, 0, 115, 30, "チェスト名", 14);
            var nameBg = Panel("ChestName", chest, 120, 0, 410, 32, "3B5558"); ui.chestName = nameBg.gameObject.AddComponent<InputField>();
            var nameText = Text("Text", nameBg.transform, 10, 0, 390, 32, "", 15); ui.chestName.textComponent = nameText; ui.chestName.characterLimit = 32;
            Text("Description", chest, 0, 38, 545, 28, "通常収納 / 全域の製作材料として使用", 13);
            var delivery = Rect("DeliveryPanel", right, 0, 0, 550, 450); ui.deliveryPanel = delivery.gameObject;
            Text("Description", delivery, 0, 0, 545, 55, "森の納品 / 取り出して素材を発見\n武器・かばん・靴で冒険者を強化できます。", 14);
            ui.npcEquipmentGrid = Grid("NpcEquipment", delivery, 0, 190, 360, 75, 3, new Vector2(105, 70));
            ui.npcLabel = Text("NpcStatus", delivery, 0, 280, 540, 145, "探索中", 14);
            ui.storageGrid = Grid("StorageSlots", right, 0, 82, 550, 200, 9, new Vector2(55, 58));
            var furnace = Rect("FurnacePanel", right, 0, 0, 550, 450); ui.furnacePanel = furnace.gameObject;
            Text("Description", furnace, 0, 0, 540, 65, "鉄鉱石 ×1 + 木材 ×1 → 鉄インゴット ×1\n材料と燃料の投入・完成品の回収は手動です。", 14);
            ui.furnaceGrid = Grid("FurnaceSlots", furnace, 0, 90, 490, 80, 3, new Vector2(130, 75));
            ui.furnaceProgress = Progress("Progress", furnace, 0, 195, 430, 18);
            ui.furnaceLabel = Text("Status", furnace, 0, 235, 540, 85, "材料を待っています", 15);
            var recipePanel = Rect("RecipesPanel", right, 0, 0, 550, 470); ui.recipesPanel = recipePanel.gameObject;
            Text("Title", recipePanel, 0, 0, 540, 30, "作業台 / 利用可能数と必要数", 16);
            Scroll("RecipeScroll", recipePanel, 0, 42, 545, 425, out var recipeContent); ui.recipeContent = recipeContent;
            var vertical = recipeContent.gameObject.AddComponent<VerticalLayoutGroup>(); vertical.childControlHeight = true; vertical.childControlWidth = true; vertical.childForceExpandHeight = false; vertical.spacing = 6;
            var fitter = recipeContent.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var discovery = Rect("DiscoveryPanel", right, 0, 0, 550, 470); ui.discoveryPanel = discovery.gameObject;
            Scroll("DiscoveryScroll", discovery, 0, 0, 545, 465, out var discoveryContent);
            ui.discoveryLabel = Text("Discovered", discoveryContent, 8, 8, 510, 600, "発見済みのアイテム", 16); ui.discoveryLabel.alignment = TextAnchor.UpperLeft;
            var discoverySize = discoveryContent.gameObject.AddComponent<ContentSizeFitter>(); discoverySize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var discoveryLayout = discoveryContent.gameObject.AddComponent<VerticalLayoutGroup>(); discoveryLayout.padding = new RectOffset(8, 8, 8, 8); discoveryLayout.childControlHeight = true; discoveryLayout.childForceExpandHeight = false;
            var pause = Panel("PauseOverlay", root.transform, 0, 0, 1280, 800, "10252B"); Stretch(pause.rectTransform); ui.pausePanel = pause.gameObject;
            var pauseCard = Panel("Menu", pause.transform, 390, 150, 500, 490, "2B4448");
            Text("Title", pauseCard.transform, 40, 27, 420, 50, "ひと休み / PAUSED", 25);
            Text("Description", pauseCard.transform, 40, 87, 420, 55, "探索・納品・加工は停止しています。\n再開すると元の画面へ戻ります。", 14);
            ui.resumeButton = Button("Resume", pauseCard.transform, 40, 160, 420, 52, "再開"); ui.saveButton = Button("Save", pauseCard.transform, 40, 228, 420, 52, "保存"); ui.quitButton = Button("SaveAndQuit", pauseCard.transform, 40, 296, 420, 52, "保存して終了");
            ui.pauseSaveLabel = Text("SaveStatus", pauseCard.transform, 40, 375, 420, 65, "未保存", 14);
            var recovery = Panel("RecoveryOverlay", root.transform, 0, 0, 1280, 800); Stretch(recovery.rectTransform); ui.recoveryPanel = recovery.gameObject;
            Text("Title", recovery.transform, 260, 160, 760, 55, "保存データを読み込めませんでした", 25);
            ui.recoveryLabel = Text("Error", recovery.transform, 260, 230, 760, 100, "元のデータを保持しています。", 16);
            ui.recoverButton = Button("RestoreBackup", recovery.transform, 260, 350, 760, 55, "直前のバックアップから復旧");
            ui.newGameButton = Button("ArchiveAndRestart", recovery.transform, 260, 430, 760, 55, "元データを退避して新規開始");
            var cursor = Panel("CursorItem", root.transform, 0, 0, 190, 60, "796841"); ui.cursorPanel = cursor.gameObject;
            cursor.rectTransform.anchorMin = cursor.rectTransform.anchorMax = cursor.rectTransform.pivot = new Vector2(.5f, .5f);
            var cursorGroup = cursor.gameObject.AddComponent<CanvasGroup>(); cursorGroup.blocksRaycasts = false; cursorGroup.interactable = false;
            ui.cursorIcon = Panel("Icon", cursor.transform, 8, 12, 32, 32, "DEB878"); ui.cursorIcon.preserveAspect = true;
            ui.cursorLabel = Text("Label", cursor.transform, 48, 6, 134, 48, "", 14);
            // Initial prefab preview shows editable inventory; runtime selects the appropriate panels.
            ui.chestPanel.SetActive(false); ui.deliveryPanel.SetActive(false); ui.furnacePanel.SetActive(false); ui.recipesPanel.SetActive(false);
            ui.storageGrid.gameObject.SetActive(false); ui.pausePanel.SetActive(false); ui.recoveryPanel.SetActive(false); ui.cursorPanel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, UiPath); UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
