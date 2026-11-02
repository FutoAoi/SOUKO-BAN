using System;
using UnityEngine;
using UnityEngine.UI;

namespace Warehouse
{
    public sealed class WarehouseRecipeView : MonoBehaviour
    {
        public Image icon;
        public Text title, materials;
        public Button craft;
        public Transform ingredientRoot;
        public WarehouseIngredientView ingredientTemplate;
        readonly System.Collections.Generic.List<WarehouseIngredientView> ingredients = new System.Collections.Generic.List<WarehouseIngredientView>();
        WarehouseGame game;
        RecipeDef recipe;
        public void Bind(WarehouseGame game, RecipeDef recipe)
        {
            this.game = game; this.recipe = recipe;
            foreach (var view in ingredients) Destroy(view.gameObject);
            ingredients.Clear();
            if (ingredientTemplate != null)
            {
                ingredientTemplate.gameObject.SetActive(false);
                foreach (var ingredient in recipe.ingredients)
                {
                    var view = Instantiate(ingredientTemplate, ingredientRoot);
                    view.gameObject.SetActive(true); ingredients.Add(view);
                }
            }
            craft.onClick.RemoveAllListeners(); craft.onClick.AddListener(() => game.Model.Craft(recipe, game.Opened)); Refresh();
        }
        public void Refresh()
        {
            if (game.Model == null) return;
            bool known = game.Model.state.unlocked.Contains(recipe.output);
            icon.enabled = known;
            if (ingredientRoot != null) ingredientRoot.gameObject.SetActive(known);
            if (known)
            {
                var d = game.Config.Item(recipe.output); icon.sprite = d.icon;
                ColorUtility.TryParseHtmlString("#" + d.color, out var c); icon.color = d.icon != null ? Color.white : c;
                title.text = d.name + " ×" + recipe.count;
                materials.text = "";
                for (int i = 0; i < ingredients.Count; i++)
                {
                    var ingredient = recipe.ingredients[i];
                    ingredients[i].Refresh(game.Config.Item(ingredient.id), game.Model.Available(ingredient.id), ingredient.count);
                }
            }
            else { title.text = "？？？"; materials.text = "新しい素材を発見すると解放"; }
            craft.interactable = known && Array.TrueForAll(recipe.ingredients, c => game.Model.Available(c.id) >= c.count);
        }
    }
}
