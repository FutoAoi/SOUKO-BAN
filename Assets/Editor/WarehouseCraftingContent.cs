using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Warehouse;

namespace WarehouseEditor
{
    public static class WarehouseCraftingContent
    {
        const string Art = "Assets/Art/CraftingMaterials/";
        const string Source = "Assets/crafting_materials/crafting_materials/resources_outlined.png";
        [MenuItem("Warehouse/Apply Crafting Materials")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Playを停止してください。");
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            var db = AssetDatabase.LoadAssetAtPath<WarehouseDatabase>(WarehouseAuthoring.DatabasePath);
            var items = new List<ItemDefinition>(db.items);
            var recipes = new List<RecipeDef>(db.settings.recipes);
            Add(items, recipes, "timber_beam", "角材", "B77F4D", "", 1, 1, 0, "plank", 4, "", 0, "", 0);
            Add(items, recipes, "stone_block", "石材", "ADB4C4", "", 1, 1, 0, "stone", 4, "", 0, "", 0);
            Add(items, recipes, "iron_plate", "鉄板", "8994A8", "", 1, 1, 0, "iron_ingot", 2, "", 0, "", 0);
            Add(items, recipes, "crystal_ingot", "結晶インゴット", "8CD6ED", "", 1, 1, 0, "iron_ingot", 2, "forest_crystal", 1, "", 0);
            Add(items, recipes, "reinforced_chest", "補強チェスト", "879BB3", "chest", 2, 1, 54, "timber_beam", 6, "iron_plate", 4, "crystal_ingot", 1);
            Add(items, recipes, "stone_wall", "石の壁", "A0A6B3", "wall", 1, 1, 0, "stone_block", 2, "", 0, "", 0);
            db.items = items.ToArray(); db.settings.recipes = recipes.ToArray();
            Set(items, "wood", 0, 0); Set(items, "plank", 1, 0); Set(items, "stone", 2, 1);
            Set(items, "iron_ore", 1, 3); Set(items, "iron_ingot", 1, 5); Set(items, "forest_crystal", 2, 2);
            Set(items, "timber_beam", 7, 0); Set(items, "stone_block", 3, 1); Set(items, "iron_plate", 4, 8); Set(items, "crystal_ingot", 5, 5);
            var large = items.Find(i => i.definition.id == "chest_large");
            var chest = items.Find(i => i.definition.id == "reinforced_chest");
            if (chest.worldSprite == null) { chest.worldSprite = large.worldSprite; chest.worldSize = large.worldSize; chest.worldOffset = large.worldOffset; }
            if (chest.icon == null) chest.icon = large.icon;
            var wall = items.Find(i => i.definition.id == "stone_wall");
            var oldWall = items.Find(i => i.definition.id == "wall");
            if (wall.worldSprite == null) { wall.worldSprite = oldWall.worldSprite; wall.worldSize = oldWall.worldSize; wall.worldOffset = oldWall.worldOffset; }
            if (wall.icon == null) wall.icon = wall.worldSprite;
            EditorUtility.SetDirty(chest); EditorUtility.SetDirty(wall); EditorUtility.SetDirty(db);
            File.WriteAllText(Art + "LICENSE.txt", "Crafting Materials by Beast Pixels\nhttps://beast-pixels.itch.io/crafting-materials\nLicense: CC0 1.0 Universal\nSelected 24x24 cells extracted from resources_outlined.png.\n");
            AssetDatabase.ImportAsset(Art + "LICENSE.txt"); AssetDatabase.SaveAssets();
            Debug.Log("WAREHOUSE CRAFTING CONTENT: replaced six material icons, added six items and recipes.");
        }
        static void Add(List<ItemDefinition> items, List<RecipeDef> recipes, string id, string name, string color, string kind, int width, int height, int slots, string a, int an, string b, int bn, string c, int cn)
        {
            if (!items.Exists(i => i.definition.id == id))
            {
                string path = "Assets/Data/Items/" + id + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemDefinition>();
                    item.definition = new ItemDef { id = id, name = name, color = color, kind = kind, width = width, height = height, slots = slots };
                    AssetDatabase.CreateAsset(item, path);
                }
                items.Add(item);
            }
            if (recipes.Exists(r => r.output == id)) return;
            var costs = new List<Ingredient> { new Ingredient(a, an) };
            if (bn > 0) costs.Add(new Ingredient(b, bn)); if (cn > 0) costs.Add(new Ingredient(c, cn));
            recipes.Add(new RecipeDef { output = id, count = 1, ingredients = costs.ToArray(), discoveries = costs.ConvertAll(i => i.id).ToArray() });
        }
        static void Set(List<ItemDefinition> items, string id, int column, int row)
        {
            string path = Art + id + ".png";
            if (!File.Exists(path))
            {
                var source = new Texture2D(2, 2); var crop = new Texture2D(24, 24, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(Source))) throw new InvalidDataException(Source);
                    crop.SetPixels(source.GetPixels(column * 24, source.height - (row + 1) * 24, 24, 24)); crop.Apply();
                    File.WriteAllBytes(path, crop.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(crop); }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var item = items.Find(i => i.definition.id == id);
            string previous = AssetDatabase.GetAssetPath(item.icon);
            if (item.icon == null || previous.StartsWith("Assets/Art/NinjaWarehouse/", StringComparison.Ordinal) || previous.StartsWith(Art, StringComparison.Ordinal))
            { item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path); EditorUtility.SetDirty(item); }
        }
    }
}
