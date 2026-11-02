using System;
using UnityEngine;

namespace Warehouse
{
    [Serializable] public class CharacterArt
    {
        [Tooltip("下・上・左・右の順。各方向は待機、歩行1、歩行2。")]
        public Sprite[] down, up, left, right;
        public Sprite Frame(int direction, int frame)
        {
            var frames = direction == 1 ? up : direction == 2 ? left : direction == 3 ? right : down;
            return frames == null || frames.Length == 0 ? null : frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }
    }
    [CreateAssetMenu(menuName = "Warehouse/World Art", fileName = "WarehouseWorldArt")]
    public sealed class WarehouseWorldArt : ScriptableObject
    {
        public Sprite floor, entrance;
        public Color floorTint = Color.white;
        public CharacterArt player = new CharacterArt(), npc = new CharacterArt();
        public Vector2 characterSize = new Vector2(.85f, .85f);
        public Vector2 characterOffset = new Vector2(0, .1f);
        [Min(1)] public float walkFramesPerSecond = 6;
    }
}
