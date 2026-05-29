using UnityEngine;

[CreateAssetMenu(fileName = "TileDefinition", menuName = "MR KIM/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public Sprite sprite;
    public float weight = 1f;
    public float Width => sprite != null ? sprite.bounds.size.x : 1f;
    // Distance from the sprite's pivot to its left edge — correct for any pivot setting.
    public float PivotOffsetX => sprite != null ? -sprite.bounds.min.x : Width * 0.5f;
    public int minWallsBefore = 0;
    public bool isDefault = false;
    public bool isSeparator = false;
    public bool isEnd = false;

    // Event tiles are reserved for specific zones: excluded from the gray
    // weighted-random fill, and matched to a zone by eventType.
    public bool isEventTile = false;
    public ZoneInputType eventType = ZoneInputType.Talk;
    // Color of the challenge overlay drawn over this event tile.
    public Color overlayColor = Color.white;
}
