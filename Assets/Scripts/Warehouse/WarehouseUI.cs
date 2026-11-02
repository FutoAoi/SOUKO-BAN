using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Warehouse
{
    public sealed class WarehouseUI : MonoBehaviour
    {
        [HideInInspector] public int authoringVersion;
        [Header("Scene connection")]
        public WarehouseGame game;
        public Canvas canvas;
        [Header("Reusable prefabs")]
        public WarehouseSlotView slotPrefab;
        public WarehouseRecipeView recipePrefab;
        [Header("Panels (layout is authored in the prefab)")]
        public GameObject inventoryPanel, chestPanel, deliveryPanel, furnacePanel, recipesPanel, discoveryPanel, pausePanel, recoveryPanel, helpPanel, cursorPanel;
        public CanvasGroup gameplayGroup;
        public Transform hotbarGrid, inventoryGrid, playerEquipmentGrid, storageGrid, npcEquipmentGrid, furnaceGrid, recipeContent;
        [Header("Text and images")]
        public Text messageLabel, saveLabel, titleLabel, capacityLabel, selectedLabel, promptLabel, npcLabel, furnaceLabel, discoveryLabel, pauseSaveLabel, recoveryLabel, cursorLabel;
        public InputField chestName;
        public Image furnaceProgress, dismantleProgress, cursorIcon;
        [Header("Buttons")]
        public Button inventoryButton, interactButton, pauseButton, helpButton, closeButton, upgradeButton, resumeButton, saveButton, quitButton, recoverButton, newGameButton;
        [Tooltip("Optional font override. Individual Text components can also have their own font.")]
        public Font fontOverride;
        [Header("World labels")]
        public RectTransform worldLabelRoot;
        public Text worldLabelPrefab;
        readonly Dictionary<string, Text> worldLabels = new Dictionary<string, Text>();
        readonly List<WarehouseSlotView> slots = new List<WarehouseSlotView>();
        readonly List<WarehouseRecipeView> recipes = new List<WarehouseRecipeView>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        Furniture boundFurniture;
        int boundInventoryCount = -1;
        bool boundOpen;
        Font fallback;
        bool showHelp;

        public void Bind(WarehouseGame game)
        {
            this.game = game;
            inventoryButton.onClick.AddListener(game.ToggleInventory); interactButton.onClick.AddListener(game.Interact);
            pauseButton.onClick.AddListener(game.TogglePause); closeButton.onClick.AddListener(game.CloseUi);
            upgradeButton.onClick.AddListener(game.UseUpgrade); resumeButton.onClick.AddListener(game.Resume);
            saveButton.onClick.AddListener(game.Save); quitButton.onClick.AddListener(game.SaveAndQuit);
            showHelp = helpPanel.activeSelf;
            helpButton.onClick.AddListener(() => showHelp = !showHelp);
            recoverButton.onClick.AddListener(() => game.Recover(true)); newGameButton.onClick.AddListener(() => game.Recover(false));
            chestName.onValueChanged.AddListener(value => { if (game.Opened != null && game.Config.Item(game.Opened.id).kind == "chest") game.Opened.name = value; });
            foreach (var r in game.Config.recipes) { var row = Instantiate(recipePrefab, recipeContent); row.Bind(game, r); recipes.Add(row); }
            ApplyFonts(); RefreshStructure();
        }
        void ApplyFonts()
        {
            if (fallback == null) fallback = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 16);
            foreach (var t in GetComponentsInChildren<Text>(true)) if (fontOverride != null || t.font == null || t.font.name == "LegacyRuntime") t.font = fontOverride != null ? fontOverride : fallback;
        }
        public bool IsPointerOverUI(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
            foreach (var hit in hits) if (hit.gameObject.transform.IsChildOf(transform)) return true;
            return false;
        }
        void ClearSlots()
        {
            foreach (var slot in slots) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
            slots.Clear();
        }
        void AddSlots(Transform parent, List<Stack> owner, int start, int length, string[] hints = null)
        {
            for (int i = 0; i < length; i++)
            {
                var slot = Instantiate(slotPrefab, parent);
                slot.Bind(game, owner, start + i, hints != null ? hints[i] : ""); slots.Add(slot);
            }
        }
        void RefreshStructure()
        {
            if (game.Model == null) return;
            ClearSlots(); var s = game.Model.state; var f = game.Opened;
            AddSlots(hotbarGrid, s.inventory, 0, 9, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
            AddSlots(inventoryGrid, s.inventory, 9, s.inventory.Count - 9);
            AddSlots(playerEquipmentGrid, s.equipment, 0, 2, new[] { "移動靴", "作業手袋" });
            string kind = f == null ? "" : game.Config.Item(f.id).kind;
            chestPanel.SetActive(kind == "chest"); deliveryPanel.SetActive(kind == "delivery"); furnacePanel.SetActive(kind == "furnace"); recipesPanel.SetActive(kind == "workbench"); discoveryPanel.SetActive(f == null);
            storageGrid.gameObject.SetActive(kind == "chest" || kind == "delivery");
            if (kind == "chest" || kind == "delivery") AddSlots(storageGrid, f.slots, 0, f.slots.Count);
            if (kind == "chest") chestName.SetTextWithoutNotify(f.name);
            if (kind == "delivery") AddSlots(npcEquipmentGrid, f.equipment, 0, 3, new[] { "武器", "かばん", "靴" });
            if (kind == "furnace") AddSlots(furnaceGrid, f.slots, 0, 3, new[] { "材料", "燃料", "完成品" });
            boundFurniture = f; boundInventoryCount = s.inventory.Count; boundOpen = game.UiOpen;
            ApplyFonts();
        }
        void LateUpdate()
        {
            if (game == null) return;
            recoveryPanel.SetActive(game.Recovery); pausePanel.SetActive(game.Paused && !game.Recovery);
            gameplayGroup.interactable = !game.Paused && !game.Recovery;
            gameplayGroup.blocksRaycasts = !game.Paused && !game.Recovery;
            recoveryLabel.text = game.RecoveryError;
            if (game.Model == null) return;
            helpPanel.SetActive(showHelp && !game.UiOpen && !game.Paused);
            if (boundFurniture != game.Opened || boundInventoryCount != game.Model.state.inventory.Count || boundOpen != game.UiOpen) RefreshStructure();
            inventoryPanel.SetActive(game.UiOpen);
            var g = game.Model; var s = g.state; var f = game.Opened;
            messageLabel.text = g.message; saveLabel.text = game.SaveStatus; pauseSaveLabel.text = game.SaveStatus;
            titleLabel.text = f == null ? "所持品と発見したアイテム" : f.name;
            capacityLabel.text = "所持品 " + s.inventory.Count + "枠";
            var selected = s.inventory[s.selected]; selectedLabel.text = selected.Empty ? "家具を選んで左クリックで設置" : game.Config.Item(selected.id).name;
            var nearest = g.Nearest(); promptLabel.text = !game.UiOpen && nearest != null ? "E  " + nearest.name + " を開く" : "";
            RefreshWorldLabels();
            interactButton.interactable = !game.UiOpen && nearest != null;
            foreach (var slot in slots) slot.Refresh();
            if (recipesPanel.activeInHierarchy) foreach (var row in recipes) row.Refresh();
            if (f != null && g.HasNpc(f))
            {
                var n = f.npc; int count = 0; foreach (var item in n.cargo) count += item.count;
                npcLabel.text = NpcStatus(g, f) + "\n探索：" + n.rolls + "個 / " + n.duration + "秒 / レア " + (n.armed ? "2%" : "1%") + "\n未納品の荷物：" + count + "個\n装備変更は次の出発から反映";
            }
            if (f != null && game.Config.Item(f.id).kind == "furnace")
            {
                furnaceProgress.fillAmount = Mathf.Clamp01(f.work / game.Config.furnaceSeconds);
                furnaceLabel.text = f.processing ? (f.work >= game.Config.furnaceSeconds ? "完成 / 出力待ち" : "加工中 " + f.work.ToString("F1") + " / " + game.Config.furnaceSeconds) : "材料・燃料・出力の空きを待っています";
            }
            if (discoveryPanel.activeInHierarchy)
            {
                string text = "発見済み " + s.discovered.Count + " / " + game.Config.items.Length + "\n\n";
                foreach (var id in s.discovered) text += "◆ " + game.Config.Item(id).name + "\n";
                discoveryLabel.text = text;
            }
            dismantleProgress.transform.parent.gameObject.SetActive(game.DismantleProgress > 0);
            dismantleProgress.fillAmount = game.DismantleProgress;
            cursorPanel.SetActive(!s.cursor.Empty && !game.Paused);
            if (!s.cursor.Empty)
            {
                var d = game.Config.Item(s.cursor.id); cursorLabel.text = d.name + " ×" + s.cursor.count; cursorIcon.sprite = d.icon;
                ColorUtility.TryParseHtmlString("#" + d.color, out var c); cursorIcon.color = d.icon != null ? Color.white : c;
                if (Mouse.current != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, Mouse.current.position.ReadValue(), null, out var point);
                    ((RectTransform)cursorPanel.transform).anchoredPosition = point + new Vector2(95, -32);
                }
            }
        }
        public static string NpcStatus(GameModel model, Furniture f)
        {
            var n = f.npc;
            return n.phase == "explore" ? "探索中 " + Mathf.CeilToInt(Mathf.Max(0, n.duration - n.elapsed)) + "秒" : n.phase == "depart" ? "出発中" : model.Distance(f, n.position) <= .6f ? "納品待ち" : "帰還中 / 通路待ち";
        }
        void RefreshWorldLabels()
        {
            if (worldLabelRoot == null || worldLabelPrefab == null) return;
            worldLabelRoot.gameObject.SetActive(!game.UiOpen && !game.Paused);
            var live = new HashSet<string>();
            foreach (var f in game.Model.state.furniture)
            {
                live.Add(f.uid);
                if (!worldLabels.TryGetValue(f.uid, out var label))
                {
                    label = Instantiate(worldLabelPrefab, worldLabelRoot); worldLabels.Add(f.uid, label);
                    if (fontOverride != null || label.font == null || label.font.name == "LegacyRuntime") label.font = fontOverride != null ? fontOverride : fallback;
                }
                var d = game.Config.Item(f.id);
                Vector2 screen = game.View.WorldToScreenPoint(new Vector3(f.x + (d.width - 1) / 2f, f.y + d.height - .25f, 0));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(worldLabelRoot, screen, null, out var point);
                label.rectTransform.anchoredPosition = point;
                label.text = f.name + (game.Model.HasNpc(f) ? "\n" + NpcStatus(game.Model, f) : "");
            }
            var remove = new List<string>();
            foreach (var pair in worldLabels) if (!live.Contains(pair.Key)) { Destroy(pair.Value.gameObject); remove.Add(pair.Key); }
            foreach (var key in remove) worldLabels.Remove(key);
        }
    }
}
