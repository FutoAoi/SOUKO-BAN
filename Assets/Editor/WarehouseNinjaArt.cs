using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseNinjaArt
    {
        const string Source = "Assets/Ninja Adventure - Asset Pack/Ninja Adventure - Asset Pack/";
        const string Art = "Assets/Art/NinjaWarehouse/";
        const string Interior = "Backgrounds/Tilesets/Interior/TilesetInteriorFloor.png";
        const string Props = "Backgrounds/Tilesets/TilesetElement.png";
        public const string AssetPath = "Assets/Resources/WarehouseWorldArt.asset";

        [MenuItem("Warehouse/Apply Ninja World Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Playを停止してから実行してください。");
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            var art = AssetDatabase.LoadAssetAtPath<WarehouseWorldArt>(AssetPath);
            if (art == null) { art = ScriptableObject.CreateInstance<WarehouseWorldArt>(); AssetDatabase.CreateAsset(art, AssetPath); }
            art.floor = Slice("StoneFloor", Interior, 12 * 16, 13 * 16, 16, 16);
            art.entrance = Slice("Entrance", Interior, 16, 16, 16, 16);
            art.player = Character("Player", "Boy"); art.npc = Character("Adventurer", "Hunter");
            art.characterSize = Vector2.one * .9f; art.characterOffset = new Vector2(0, .15f); art.walkFramesPerSecond = 6;
            SetFurniture("chest_small", Slice("SmallTreasureChest", "Items/Treasure/LittleTreasureChest.png", 0, 0, 16, 16), new Vector2(.95f, .95f), Vector2.zero);
            SetFurniture("chest_large", Slice("LargeTreasureChest", "Items/Treasure/BigTreasureChest.png", 0, 0, 16, 14), new Vector2(1.7f, 1.1f), new Vector2(0, .1f));
            SetFurniture("workbench", Slice("Workbench", Props, 11 * 16, 16, 32, 16), new Vector2(1.95f, 1), Vector2.zero);
            SetFurniture("forest_delivery", Slice("DeliveryShelf", Props, 8 * 16, 7 * 16, 32, 32), new Vector2(1.95f, 2), new Vector2(0, .1f));
            SetFurniture("furnace", Slice("Furnace", Props, 4 * 16, 7 * 16, 16, 32), new Vector2(.95f, 1.8f), new Vector2(0, .4f));
            SetFurniture("wall", Slice("StoneWall", Interior, 16, 0, 16, 16), Vector2.one, Vector2.zero);
            SetIcon("wood", FullSprite("Wood", "Items/Resource/Branch.png"));
            SetIcon("stone", FullSprite("Stone", "Items/Resource/Rock.png"));
            SetIcon("forest_crystal", FullSprite("ForestCrystal", "Items/Resource/GemGreen.png"));
            SetIcon("iron_ingot", FullSprite("IronIngot", "Items/Resource/BarIron.png"));
            SetIcon("npc_sword", FullSprite("Sword", "Items/Weapons/Sword/Sprite.png"));
            SetIcon("npc_bag", FullSprite("Bag", "Items/Object/Bag.png"));
            SetIcon("inventory_upgrade", FullSprite("InventoryCrate", "Items/Object/CrateEmpty.png"));
            SetIcon("plank", PixelIcon("Plank", new[] {"............", "..OOOOOOOO..", ".OLLLLLLLO..", ".OLDDLDLLO..", ".OLLLLLLLO..", ".OOOOOOOOO..", "...OOOOOOOO.", "..OLLLLLLLO.", "..OLDDLDLLO.", "..OLLLLLLLO.", "..OOOOOOOOO.", "............"}, "62402B", "D9A45D", "A76B3E"));
            SetIcon("iron_ore", PixelIcon("IronOre", new[] {"............", "....OOOO....", "...OLLLLO...", "..OLLDLLLO..", ".OLLLDDLLLO.", ".OLDDLLDLLO.", "OLLLLDLLLDLO", "OLDDLLLLDDLO", "ODLLDDLLLDDO", ".ODDDDDDDDO.", "..OOOOOOOO..", "............"}, "343D45", "9CAAB0", "BA8050"));
            var boots = PixelIcon("Boots", new[] {"............", ".OOOO..OOOO.", ".OLLO..OLLO.", ".OLLO..OLLO.", ".ODDO..ODDO.", ".OLLO..OLLO.", ".OLLO..OLLO.", ".OLLO..OLLO.", ".OLLLLOLLLLO", ".OLLLLOLLLLO", ".OOOOOOOOOO.", "............"}, "332D32", "AD794D", "E5BD78");
            SetIcon("npc_boots", boots); SetIcon("player_boots", boots);
            SetIcon("work_gloves", PixelIcon("WorkGloves", new[] {"............", "..OO...OO...", ".OLLO.OLLO..", ".OLLO.OLLO..", ".OLLOOOLLO..", "OOLLLLLLLOO.", "OLLLLLLLLLLO", "OLLDLLDLLLLO", ".OLLLLLLLLO.", "..ODDOODDO..", "..OOOOOOOO..", "............"}, "43372F", "DAB076", "9C6844"));
            EditorUtility.SetDirty(art);
            if (!File.Exists(Art + "LICENSE.txt")) File.Copy(Source + "LICENSE.txt", Art + "LICENSE.txt");
            AssetDatabase.ImportAsset(Art + "LICENSE.txt"); AssetDatabase.SaveAssets();
            Debug.Log("WAREHOUSE NINJA ART APPLIED: floor, directional animated characters and six furniture sprites.");
        }
        static CharacterArt Character(string name, string source)
        {
            var directions = new Sprite[4][];
            for (int d = 0; d < 4; d++)
            {
                string folder = "Actor/Character/" + source + "/SeparateAnim/";
                directions[d] = new[]
                {
                    Slice(name + "_" + d + "_Idle", folder + "Idle.png", d * 16, 0, 16, 16),
                    Slice(name + "_" + d + "_Walk1", folder + "Walk.png", d * 16, 0, 16, 16),
                    Slice(name + "_" + d + "_Walk2", folder + "Walk.png", d * 16, 16, 16, 16)
                };
            }
            return new CharacterArt { down = directions[0], up = directions[1], left = directions[2], right = directions[3] };
        }
        static void SetIcon(string id, Sprite sprite)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/" + id + ".asset");
            if (item == null) throw new InvalidOperationException("アイテムSOがありません：" + id);
            if (item.icon == null || AssetDatabase.GetAssetPath(item.icon).StartsWith(Art, StringComparison.Ordinal))
            { item.icon = sprite; EditorUtility.SetDirty(item); }
        }
        static Sprite FullSprite(string name, string source)
        {
            if (File.Exists(Art + name + ".png")) return Slice(name, source, 0, 0, 1, 1);
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(Source + source))) throw new InvalidDataException(source);
                return Slice(name, source, 0, 0, texture.width, texture.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        // Small project-owned pixel symbols fill gaps in the free pack; source stays editable here.
        static Sprite PixelIcon(string name, string[] rows, string outline, string light, string dark)
        {
            string path = Art + name + ".png";
            if (!File.Exists(path))
            {
                int width = rows[0].Length, height = rows.Length;
                ColorUtility.TryParseHtmlString("#" + outline, out var o); ColorUtility.TryParseHtmlString("#" + light, out var l); ColorUtility.TryParseHtmlString("#" + dark, out var d);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (rows[y].Length != width) throw new InvalidDataException("Pixel icon row width: " + name);
                        for (int x = 0; x < width; x++) texture.SetPixel(x, height - 1 - y, rows[y][x] == 'O' ? o : rows[y][x] == 'L' ? l : rows[y][x] == 'D' ? d : Color.clear);
                    }
                    texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            return Slice(name, "", 0, 0, 1, 1);
        }
        static void SetFurniture(string id, Sprite sprite, Vector2 size, Vector2 offset)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/" + id + ".asset");
            if (item == null) throw new InvalidOperationException("アイテムSOがありません：" + id);
            item.worldSprite = sprite; item.worldSize = size; item.worldOffset = offset;
            if (item.icon == null || AssetDatabase.GetAssetPath(item.icon).StartsWith(Art, StringComparison.Ordinal)) item.icon = sprite;
            EditorUtility.SetDirty(item);
        }
        static Sprite Slice(string name, string source, int x, int top, int width, int height)
        {
            string path = Art + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(Source + source))) throw new InvalidDataException(source);
                    if (x < 0 || top < 0 || x + width > texture.width || top + height > texture.height) throw new InvalidDataException("Sprite crop out of bounds: " + source);
                    crop.SetPixels(texture.GetPixels(x, texture.height - top - height, width, height)); crop.Apply(); File.WriteAllBytes(path, crop.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(crop); }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

