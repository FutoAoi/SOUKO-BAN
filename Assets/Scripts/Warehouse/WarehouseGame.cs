using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warehouse
{
    public class WarehouseGame : MonoBehaviour
    {
        [SerializeField] public WarehouseUI ui;
        [SerializeField] public WarehouseWorldArt worldArt;
        public GameModel Model { get; private set; }
        public GameConfig Config => config;
        public Furniture Opened => opened;
        public bool UiOpen => inventoryOpen || opened != null;
        public bool Paused => paused;
        public bool Recovery => recovery;
        public string SaveStatus => saveStatus;
        public string RecoveryError => recoveryError;
        public Camera View => view;
        public float DismantleProgress => dismantling == null ? 0 : Mathf.Clamp01(dismantle / config.dismantleSeconds);
        GameConfig config;
        Furniture opened, dismantling;
        bool inventoryOpen, paused, recovery;
        float dismantle, autosave;
        string saveStatus = "未保存", recoveryError;
        Camera view;
        Sprite pixel;
        readonly Dictionary<string, SpriteRenderer> visuals = new Dictionary<string, SpriteRenderer>();
        readonly HashSet<string> activeVisuals = new HashSet<string>();
        readonly List<SlotRef> drag = new List<SlotRef>();
        readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        readonly Dictionary<KeyCode, float> commandTimes = new Dictionary<KeyCode, float>();
        SlotRef hovered;
        bool dragging, rightDrag;
        Vector2 dragOrigin;
        public sealed class SlotRef
        {
            public List<Stack> owner; public int index; public Stack Value => owner[index];
            public bool Same(SlotRef other) => other != null && other.owner == owner && other.index == index;
        }
        void Awake()
        {
            config = GameConfig.Load();
            if (worldArt == null) worldArt = Resources.Load<WarehouseWorldArt>("WarehouseWorldArt");
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point }; texture.SetPixel(0, 0, Color.white); texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            view = Camera.main;
            if (view == null) view = new GameObject("Warehouse Camera", typeof(Camera)).GetComponent<Camera>();
            view.orthographic = true; view.orthographicSize = 7.7f; view.backgroundColor = Hex("16282B"); view.clearFlags = CameraClearFlags.SolidColor;
            if (File.Exists(SaveStore.Path))
            {
                try { Model = new GameModel(config, SaveStore.Load(SaveStore.Path, config)); }
                catch (Exception e) { recovery = true; paused = true; recoveryError = e.Message; }
            }
            else Model = new GameModel(config);
        }
        void Start()
        {
            if (ui == null) ui = FindFirstObjectByType<WarehouseUI>();
            if (ui == null) { Debug.LogError("WarehouseUI Prefabをシーンへ配置してください。", this); enabled = false; return; }
            ui.Bind(this);
            if (Model != null) SyncWorld();
        }
        void Update()
        {
            var k = Keyboard.current; var mouse = Mouse.current;
            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) Command(KeyCode.Escape);
                if (k.tabKey.wasPressedThisFrame) Command(KeyCode.Tab);
                if (k.eKey.wasPressedThisFrame) Command(KeyCode.E);
                var keys = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key, k.digit7Key, k.digit8Key, k.digit9Key };
                for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame) Command((KeyCode)((int)KeyCode.Alpha1 + i));
            }
            if (recovery || Model == null || paused) return;
            if (!UiOpen) Model.Move(new Vector2(((k != null && k.dKey.isPressed) || heldKeys.Contains(KeyCode.D) ? 1 : 0) - ((k != null && k.aKey.isPressed) || heldKeys.Contains(KeyCode.A) ? 1 : 0), ((k != null && k.wKey.isPressed) || heldKeys.Contains(KeyCode.W) ? 1 : 0) - ((k != null && k.sKey.isPressed) || heldKeys.Contains(KeyCode.S) ? 1 : 0)), Time.deltaTime);
            Model.Tick(Time.deltaTime);
            autosave += Time.deltaTime; if (autosave >= config.saveSeconds) { autosave = 0; Save(); }
            if (!UiOpen && mouse != null)
            {
                Vector2 p = view.ScreenToWorldPoint(mouse.position.ReadValue()); int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                bool overHud = ui != null && ui.IsPointerOverUI(mouse.position.ReadValue());
                if (!overHud && mouse.leftButton.wasPressedThisFrame) Model.Place(x, y);
                Furniture target = overHud ? null : Model.At(x, y);
                if (mouse.rightButton.isPressed && target != null && Model.Distance(target, Model.state.player) <= config.interactDistance)
                {
                    if (dismantling != target) { dismantling = target; dismantle = 0; }
                    dismantle += Time.deltaTime * Model.WorkSpeed;
                    if (dismantle >= config.dismantleSeconds) { Model.Dismantle(target); dismantling = null; dismantle = 0; }
                }
                else { dismantling = null; dismantle = 0; }
            }
            else { dismantling = null; dismantle = 0; }
            SyncWorld();
        }
        // Input compatibility only: all rendering is serialized uGUI, with no IMGUI controls.
        void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyUp) { heldKeys.Remove(e.keyCode); return; }
            if (e.type == EventType.KeyDown && heldKeys.Add(e.keyCode)) Command(e.keyCode);
        }
        void Command(KeyCode key)
        {
            if (commandTimes.TryGetValue(key, out float at) && Time.unscaledTime - at < .1f) return;
            commandTimes[key] = Time.unscaledTime;
            if (recovery || Model == null) return;
            if (key == KeyCode.Escape) { TogglePause(); return; }
            if (paused) return;
            if (key == KeyCode.Tab) { ToggleInventory(); return; }
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() is InputField field && field.isFocused) return;
            if (key == KeyCode.E) { Interact(); return; }
            int n = (int)key - (int)KeyCode.Alpha1;
            if (n >= 0 && n < 9 && Model.state.cursor.Empty) { if (UiOpen && hovered != null) SwapHotbar(n); else Model.state.selected = n; }
        }
        public void Interact() { if (paused || recovery) return; if (UiOpen) CloseUi(); else { opened = Model.Nearest(); inventoryOpen = opened != null; } }
        public void ToggleInventory() { if (paused || recovery) return; if (UiOpen) CloseUi(); else inventoryOpen = true; }
        public void CloseUi() { CancelDrag(); Model.ReturnCursor(); opened = null; inventoryOpen = false; hovered = null; }
        public void TogglePause() { if (recovery) return; paused = !paused; CancelDrag(); }
        public void Resume() { if (!recovery) paused = false; }
        public void UseUpgrade() { var s = Model.state.inventory.Find(a => a.id == "inventory_upgrade" && !a.Empty); if (s != null) Model.UseUpgrade(s); else Model.message = "所持枠拡張アイテムが必要です。"; }
        public void SetHovered(SlotRef slot)
        {
            hovered = slot;
            if (dragging && slot != null && !drag.Exists(a => a.Same(slot))) drag.Add(slot);
        }
        public void ClearHovered(SlotRef slot) { if (hovered != null && hovered.Same(slot)) hovered = null; }
        public void SlotDown(SlotRef slot, bool right, bool shift, Vector2 pointer)
        {
            if (paused || recovery) return;
            if (!UiOpen) { if (slot.owner == Model.state.inventory && slot.index < 9) Model.state.selected = slot.index; return; }
            if (shift && Model.state.cursor.Empty) QuickMove(slot);
            else if (Model.state.cursor.Empty) Click(slot, right);
            else { dragging = true; rightDrag = right; dragOrigin = pointer; drag.Clear(); drag.Add(slot); }
        }
        public void FinishSlotAction(Vector2 pointer)
        {
            if (!dragging || paused || Model.state.cursor.Empty) { CancelDrag(); return; }
            if (drag.Count == 1 && Vector2.Distance(pointer, dragOrigin) < 8) Click(drag[0], rightDrag);
            else
            {
                var slots = new List<Stack>();
                foreach (var r in drag) if (Model.Accepts(r.owner, r.index, Model.state.cursor.id, opened)) slots.Add(r.Value);
                Inventory.Distribute(slots, Model.state.cursor, rightDrag, config, (s, id) => true);
            }
            CancelDrag();
        }
        void SwapHotbar(int i)
        {
            var from = hovered; var target = Model.state.inventory[i];
            if (!target.Empty && !Model.Accepts(from.owner, from.index, target.id, opened)) return;
            if (from.owner == Model.state.inventory && from.index == i) return;
            var old = from.Value.Copy(); from.Value.id = target.id; from.Value.count = target.count; target.id = old.id; target.count = old.count;
            if (!old.Empty) Model.Received(from.owner, from.index, old.id, opened);
        }
        void CancelDrag() { dragging = false; drag.Clear(); }
        void OnApplicationFocus(bool focused) { if (!focused) { heldKeys.Clear(); CancelDrag(); } }
        public void Save()
        {
            if (Model == null || recovery) return;
            try { SaveStore.Save(Model.state); saveStatus = "保存完了 " + DateTime.Now.ToString("HH:mm:ss"); }
            catch (Exception e) { saveStatus = "保存失敗：" + e.Message; Debug.LogError(saveStatus); }
        }
        public void SaveAndQuit()
        {
            Save(); if (saveStatus.StartsWith("保存失敗")) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnApplicationQuit() { Save(); }
        void OnApplicationPause(bool value) { if (value) Save(); }
        static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }
        void Visual(string id, Vector2 pos, Vector2 size, Color color, int order, Sprite sprite = null)
        {
            activeVisuals.Add(id);
            if (!visuals.TryGetValue(id, out var r)) { r = new GameObject(id).AddComponent<SpriteRenderer>(); r.transform.SetParent(transform); r.sprite = pixel; visuals[id] = r; }
            r.sprite = sprite != null ? sprite : pixel;
            r.transform.position = new Vector3(pos.x, pos.y, 0); r.transform.localScale = new Vector3(size.x / r.sprite.bounds.size.x, size.y / r.sprite.bounds.size.y, 1); r.color = color; r.sortingOrder = order;
        }
        void SyncWorld()
        {
            activeVisuals.Clear(); var s = Model.state;
            bool texturedFloor = worldArt != null && worldArt.floor != null;
            for (int x = 0; x < s.width; x++) for (int y = 0; y < s.height; y++) Visual("tile" + x + ":" + y, new Vector2(x, y), Vector2.one * (texturedFloor ? 1 : .97f), texturedFloor ? worldArt.floorTint : (x + y) % 2 == 0 ? Hex("2E4545") : Hex("314949"), -10000, texturedFloor ? worldArt.floor : null);
            Visual("entry", Model.Entrance, Vector2.one * .95f, worldArt != null && worldArt.entrance != null ? Color.white : Hex("74B8A2"), -9999, worldArt != null ? worldArt.entrance : null);
            var nearest = Model.Nearest();
            foreach (var f in s.furniture)
            {
                var d = config.Item(f.id); var size = new Vector2(d.width, d.height); var center = new Vector2(f.x, f.y) + (size - Vector2.one) / 2;
                if (d.worldSprite != null)
                {
                    int order = WorldOrder(f.y - .35f);
                    Visual(f.uid + "shadow", center + new Vector2(0, -.18f), new Vector2(size.x * .82f, .25f), new Color(.05f, .08f, .1f, .35f), order - 1);
                    // The grid highlight marks the footprint; the art can extend above it.
                    if (f == nearest && !UiOpen) Visual(f.uid + "outline", center, size - Vector2.one * .08f, new Color(1, .83f, .42f, .3f), -9998);
                    Visual(f.uid, center + d.worldOffset, d.worldSize, Color.white, order, d.worldSprite);
                }
                else
                {
                Visual(f.uid + "shadow", center + new Vector2(.08f, -.12f), size - Vector2.one * .06f, Hex("172D30"), -3);
                Visual(f.uid + "outline", center, size - Vector2.one * .06f, f == nearest && !UiOpen ? Hex("F0DA99") : Hex("263436"), -2);
                Visual(f.uid, center, size - Vector2.one * .18f, Hex(d.color), -1);
                Visual(f.uid + "detail", center + new Vector2(0, .12f), new Vector2(size.x - .35f, .13f), Hex("E8DAB3") * new Color(1, 1, 1, .7f), 0);
                }
                if (Model.HasNpc(f) && f.npc.phase != "explore") Character(f.uid + "npc", f.npc.position, Hex("A6C8DA"));
                if (f.processing) Visual(f.uid + "fire", center + new Vector2(0, -.14f), new Vector2(.3f, .2f), Hex("FFB762"), d.worldSprite != null ? WorldOrder(f.y - .35f) + 1 : 1);
            }
            for (int i = 0; i < s.drops.Count; i++)
            {
                var drop = s.drops[i]; var item = config.Item(drop.item.id);
                Visual("drop" + i + "shadow", drop.position + new Vector2(0, -.1f), new Vector2(.35f, .1f), new Color(0, 0, 0, .25f), WorldOrder(drop.position.y) - 1);
                Visual("drop" + i, drop.position, Vector2.one * .4f, item.icon != null ? Color.white : Hex(item.color), WorldOrder(drop.position.y), item.icon);
            }
            Character("player", s.player, Hex("F1CD80"));
            if (!UiOpen && !paused && Mouse.current != null)
            {
                var selected = s.inventory[s.selected];
                if (!selected.Empty && config.Item(selected.id).Furniture)
                {
                    Vector2 p = view.ScreenToWorldPoint(Mouse.current.position.ReadValue()); int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y); var d = config.Item(selected.id); var size = new Vector2(d.width, d.height);
                    Visual("preview", new Vector2(x, y) + (size - Vector2.one) / 2, size * .95f, Model.CanPlace(selected.id, x, y) ? new Color(.4f, 1, .7f, .5f) : new Color(1, .3f, .3f, .5f), 2000);
                }
            }
            var remove = new List<string>(); foreach (var pair in visuals) if (!activeVisuals.Contains(pair.Key)) { Destroy(pair.Value.gameObject); remove.Add(pair.Key); } foreach (var id in remove) visuals.Remove(id);
            float halfX = view.orthographicSize * view.aspect;
            float cx = s.width < halfX * 2 ? (s.width - 1) / 2f : Mathf.Clamp(s.player.x, halfX - .5f, s.width - halfX - .5f);
            float cy = s.height < view.orthographicSize * 2 ? (s.height - 1) / 2f : Mathf.Clamp(s.player.y, view.orthographicSize - .5f, s.height - view.orthographicSize - .5f);
            view.transform.position = new Vector3(cx, cy, -10);
        }
        void Character(string id, Vector2 p, Color color)
        {
            var art = worldArt == null ? null : id == "player" ? worldArt.player : worldArt.npc;
            if (art != null && art.Frame(0, 0) != null)
            {
                if (!poses.TryGetValue(id, out var pose)) { pose = new CharacterPose { previous = p }; poses[id] = pose; }
                var delta = p - pose.previous; bool walking = delta.sqrMagnitude > .000001f;
                if (walking) pose.direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? 3 : 2) : (delta.y > 0 ? 1 : 0);
                pose.previous = p;
                int frame = walking ? 1 + (Mathf.FloorToInt(Time.time * worldArt.walkFramesPerSecond) % 2) : 0;
                int order = WorldOrder(p.y);
                Visual(id + "shadow", p + new Vector2(0, -.23f), new Vector2(.5f, .17f), new Color(.05f, .08f, .1f, .35f), order - 1);
                Visual(id + "sprite", p + worldArt.characterOffset, worldArt.characterSize, Color.white, order, art.Frame(pose.direction, frame)); return;
            }
            Visual(id + "shadow", p + new Vector2(0, -.19f), new Vector2(.55f, .22f), Hex("193033"), 2);
            Visual(id + "body", p, new Vector2(.42f, .5f), color, 4);
            Visual(id + "head", p + new Vector2(0, .32f), new Vector2(.32f, .29f), Hex("F2DDB2"), 5);
            Visual(id + "hair", p + new Vector2(0, .46f), new Vector2(.38f, .1f), Hex("645047"), 6);
            Visual(id + "eyes", p + new Vector2(.08f, .31f), new Vector2(.06f, .06f), Hex("233C40"), 6);
        }
        sealed class CharacterPose { public Vector2 previous; public int direction; }
        readonly Dictionary<string, CharacterPose> poses = new Dictionary<string, CharacterPose>();
        static int WorldOrder(float y) => 1000 - Mathf.RoundToInt(y * 20);
        public void Click(SlotRef slot, bool right)
        {
            string before = slot.Value.Empty ? null : slot.Value.id;
            bool pickup = Model.state.cursor.Empty;
            string movingId = pickup ? before : Model.state.cursor.id;
            int max = movingId == null ? 99 : config.Item(movingId).max;
            Inventory.Click(slot.Value, Model.state.cursor, right, max, id => Model.Accepts(slot.owner, slot.index, id, opened));
            if (before != null && (pickup || (!Model.state.cursor.Empty && Model.state.cursor.id == before))) Model.Received(slot.owner, slot.index, before, opened);
        }
        public void QuickMove(SlotRef from)
        {
            if (opened == null || from.Value.Empty) return;
            var s = from.Value; string id = s.id; int before = s.count;
            if (from.owner != Model.state.inventory) s.count = Inventory.Add(Model.state.inventory, id, s.count, config);
            else
            {
                string kind = config.Item(opened.id).kind;
                if (kind == "furnace")
                {
                    int i = id == "iron_ore" ? 0 : id == "wood" ? 1 : -1;
                    if (i >= 0) s.count = Inventory.Add(new List<Stack> { opened.slots[i] }, id, s.count, config);
                }
                else if (kind == "chest" || kind == "delivery") s.count = Inventory.Add(opened.slots, id, s.count, config);
            }
            if (s.count < before && from.owner != Model.state.inventory) Model.Received(from.owner, from.index, id, opened);
            if (s.count == 0) s.Clear();
        }
        public void Recover(bool backup)
        {
            try
            {
                SaveState loaded = backup ? SaveStore.Load(SaveStore.Path + ".bak", config) : null;
                if (File.Exists(SaveStore.Path)) File.Move(SaveStore.Path, SaveStore.Path + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff"));
                Model = new GameModel(config, loaded); recovery = false; paused = false; SyncWorld();
            }
            catch (Exception e) { recoveryError = "復旧失敗：" + e.Message; }
        }
    }
}
