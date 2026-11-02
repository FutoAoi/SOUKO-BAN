using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse
{
    [Serializable] public class ItemDef
    {
        public string id, name, equipment;
        public int max = 99, width = 1, height = 1, slots;
        public string color = "B6C7B0", kind;
        [NonSerialized] public Sprite icon;
        [NonSerialized] public Sprite worldSprite;
        [NonSerialized] public Vector2 worldSize, worldOffset;
        public bool Furniture => !string.IsNullOrEmpty(kind);
    }
    [Serializable] public class Ingredient
    {
        public string id; public int count;
        public Ingredient(string id, int count) { this.id = id; this.count = count; }
    }
    [Serializable] public class RecipeDef
    {
        public string output; public int count = 1;
        public Ingredient[] ingredients; public string[] discoveries;
    }
    [Serializable] public class GameConfig
    {
        public int width = 24, height = 24, initialSlots = 18, maxSlots = 36, upgradeSlots = 9;
        public float moveSpeed = 4, interactDistance = 1.5f, pickupDistance = .75f, dismantleSeconds = 1;
        public float saveSeconds = 60, expeditionSeconds = 20, equippedExpeditionSeconds = 16, furnaceSeconds = 8, equipmentMultiplier = 1.25f;
        public int normalRolls = 4, bagRolls = 6;
        public float woodChance = .49f, stoneChance = .30f, oreChance = .20f, armedWoodChance = .48f;
        [HideInInspector] public ItemDef[] items;
        public RecipeDef[] recipes;
        [NonSerialized] Dictionary<string, ItemDef> index;
        public ItemDef Item(string id) { if (index == null) { index = new Dictionary<string, ItemDef>(); foreach (var i in items) index.Add(i.id, i); } return index[id]; }
        public static GameConfig Load()
        {
            var database = Resources.Load<WarehouseDatabase>("WarehouseDatabase");
            if (database == null) throw new InvalidOperationException("WarehouseDatabase.asset がありません。Warehouse > Prepare Editable Assets を実行してください。");
            return database.Build();
        }
    }
}
