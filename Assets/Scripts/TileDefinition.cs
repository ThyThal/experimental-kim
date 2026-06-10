using UnityEngine;

[CreateAssetMenu(fileName = "TileDefinition", menuName = "MR KIM/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public Sprite sprite;
    [Tooltip("Optional sprite variants. When more than one, a random one (never the same as the last placed) is used. Falls back to 'sprite' when empty.")]
    public Sprite[] sprites;
    public float weight = 1f;

    // Variants share a footprint: width/pivot come from the primary sprite so
    // layout math is stable regardless of which variant is rendered.
    public Sprite PrimarySprite => (sprites != null && sprites.Length > 0) ? sprites[0] : sprite;
    public int SpriteCount => (sprites != null && sprites.Length > 0) ? sprites.Length : (sprite != null ? 1 : 0);
    public Sprite GetSprite(int index) =>
        (sprites != null && index >= 0 && index < sprites.Length) ? sprites[index] : sprite;

    public float Width => PrimarySprite != null ? PrimarySprite.bounds.size.x : 1f;
    // Distance from the sprite's pivot to its left edge — correct for any pivot setting.
    public float PivotOffsetX => PrimarySprite != null ? -PrimarySprite.bounds.min.x : Width * 0.5f;
    public int minWallsBefore = 0;
    public bool isDefault = false;
    public bool isSeparator = false;
    public bool isEnd = false;
}
