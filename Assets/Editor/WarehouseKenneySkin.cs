using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseKenneySkin
    {
        const string Source = "Assets/kenney_ui-pack-pixel-adventure/Tiles/Large tiles/Thin outline/";
        const string Art = "Assets/UI/Art/KenneyPixelAdventure/";
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var value); return value; }
        static Sprite Import(string name, int tile)
        {
            string src = Source + "tile_" + tile.ToString("D4") + ".png", dest = Art + name + ".png";
            if (!File.Exists(dest))
            {
                if (!File.Exists(src)) throw new FileNotFoundException("Kenney UI素材がありません", src);
                File.Copy(src, dest);
            }
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(dest);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.spriteBorder = new Vector4(5, 5, 5, 5);
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(dest);
        }
        static void Image(Image image, Sprite sprite)
        {
            image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = .5f; image.color = Color.white;
        }
        static void Buttons(GameObject root, Sprite sprite)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                Image(button.GetComponent<Image>(), sprite); button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = C("FFE4B1"); colors.pressedColor = C("BA996F"); colors.selectedColor = Color.white; colors.disabledColor = new Color(.7f, .7f, .7f, .6f); button.colors = colors;
                foreach (var text in button.GetComponentsInChildren<Text>(true)) { text.color = C("FFF3D6"); text.fontStyle = FontStyle.Bold; }
            }
        }
        static void PaperText(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.color = C("241B12"); text.fontStyle = FontStyle.Bold;
                if (text.fontSize < 14) text.fontSize = 14;
            }
        }
        [MenuItem("Warehouse/Apply Kenney Pixel Skin")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Playを停止してから実行してください。");
            WarehouseAuthoring.EnsureAssets(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            var panel = Import("ParchmentPanel", 13); var wood = Import("WoodButton", 1);
            var slot = Import("SlateSlot", 3); var selection = Import("GoldSelection", 19);
            var row = Import("RecipePanel", 2); var darkWood = Import("WoodPanel", 14);
            string license = Art + "License.txt";
            if (!File.Exists(license)) File.Copy("Assets/kenney_ui-pack-pixel-adventure/License.txt", license);
            AssetDatabase.ImportAsset(license);
            Edit("Assets/UI/Prefabs/InventorySlot.prefab", root =>
            {
                var view = root.GetComponent<WarehouseSlotView>(); Image(root.GetComponent<Image>(), slot); Image(view.selection, selection);
                var inset = root.transform.Find("Selection/Inset"); if (inset != null) inset.GetComponent<Image>().enabled = false;
                var countRect = view.count.rectTransform; countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(1, 0); countRect.anchoredPosition = new Vector2(-4, 0); countRect.sizeDelta = new Vector2(48, 18); view.count.fontSize = 14;
                var nameRect = view.itemName.rectTransform; nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(.5f, 0); nameRect.anchoredPosition = new Vector2(0, 14); nameRect.sizeDelta = new Vector2(54, 24); view.itemName.fontSize = 12;
                var iconRect = view.icon.rectTransform; iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, 1); iconRect.anchoredPosition = new Vector2(0, -3); iconRect.sizeDelta = new Vector2(20, 20);
                foreach (var text in root.GetComponentsInChildren<Text>(true)) text.color = C("FFF3D6");
            });
            Edit("Assets/UI/Prefabs/RecipeRow.prefab", root =>
            {
                Image(root.GetComponent<Image>(), row);
                PaperText(root);
                Buttons(root, wood);
            });
            Edit(WarehouseAuthoring.UiPath, root =>
            {
                var ui = root.GetComponent<WarehouseUI>();
                Image(ui.inventoryPanel.GetComponent<Image>(), panel);
                PaperText(ui.inventoryPanel);
                Image(ui.helpPanel.GetComponent<Image>(), panel);
                PaperText(ui.helpPanel);
                foreach (string path in new[] { "Gameplay/Header", "Gameplay/Hotbar" }) Image(root.transform.Find(path).GetComponent<Image>(), darkWood);
                Image(ui.pausePanel.transform.Find("Menu").GetComponent<Image>(), panel);
                PaperText(ui.pausePanel);
                Image(ui.cursorPanel.GetComponent<Image>(), panel); ui.cursorLabel.color = C("40362E");
                Image(ui.chestName.GetComponent<Image>(), row); ui.chestName.textComponent.color = C("29363D");
                // Viewports mask list entries; a plain paper color avoids repeated frame borders.
                foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
                {
                    var bg = scroll.viewport.GetComponent<Image>(); if (bg != null) { bg.sprite = null; bg.color = C("E7D4AF"); }
                }
                Buttons(root, wood);
            });
            AssetDatabase.SaveAssets(); Debug.Log("WAREHOUSE KENNEY SKIN APPLIED: six sprites, Point filtering, sliced panels and buttons.");
        }
        static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
