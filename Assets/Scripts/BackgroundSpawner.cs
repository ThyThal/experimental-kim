using System.Collections.Generic;
using UnityEngine;

public enum ZoneInputType
{
    Murmur,  // low sustained amplitude
    Talk,    // normal speaking amplitude
    Scream,  // high sustained amplitude
    Clap     // sharp transient peak
}

[System.Serializable]
public struct TileZone
{
    public int tileCount;
    public ZoneInputType inputType;
}

/// <summary>A point where the player stops to perform a timed voice challenge.</summary>
public readonly struct ChallengePoint
{
    public readonly float midpointX;
    public readonly ZoneInputType requiredType;

    public ChallengePoint(float midpointX, ZoneInputType requiredType)
    {
        this.midpointX = midpointX;
        this.requiredType = requiredType;
    }
}

public class BackgroundSpawner : MonoBehaviour
{
    [SerializeField] private TileDefinition[] _tileDefinitions;
    [SerializeField] private float _lookahead = 3f;
    [SerializeField] private float _recycleBuffer = 8f;
    [SerializeField] private float _tileY = 0f;
    [SerializeField] private float _tileZ = -1f;
    [SerializeField] private TileZone[] _zones;
    [SerializeField] private float _overlayZ = -0.5f;
    [Tooltip("Color painted across the whole section (the gray travel area).")]
    [SerializeField] private Color _defaultZoneColor = Color.gray;
    [Tooltip("Destroy any pre-placed child tiles/overlays at startup and generate fresh from this object's position. Prevents stale leftover tiles from pushing challenges out of the player's reach.")]
    [SerializeField] private bool _clearExistingOnStart = true;

    private float _spawnX;
    private bool _lastWasSeparator = true;
    private bool _endTriggered;
    private bool _finished;
    private Camera _camera;
    private int _contentTilesInZone;
    private int _currentZoneIndex;
    private float _currentZoneStartX;
    // Which content tile (1-based) in the current zone is the specific challenge
    // tile, and the placed span of that tile once we reach it. -1 = not chosen.
    private int _middleIndexThisZone = -1;
    private bool _hasMidTile;
    private float _midTileLeftX;
    private float _midTileWidth;
    private TileDefinition _midTileDef;
    private System.Random _rng = new();
    // Counts default tiles placed since each special type last appeared.
    private readonly Dictionary<TileDefinition, int> _wallsSince = new();
    private readonly Dictionary<TileDefinition, Queue<GameObject>> _pool = new();
    private readonly List<(GameObject go, TileDefinition def, float rightEdge)> _active = new();
    private readonly List<(GameObject go, float rightEdge)> _manualTiles = new();
    private readonly List<ChallengePoint> _challenges = new();

    /// <summary>Challenge points discovered so far, ordered left to right.</summary>
    public IReadOnlyList<ChallengePoint> Challenges => _challenges;

    private void Start()
    {
        if (_tileDefinitions == null || _tileDefinitions.Length == 0)
        {
            Debug.LogError("[BackgroundSpawner] _tileDefinitions is empty. Assign TileDefinition assets in the Inspector.", this);
            enabled = false;
            return;
        }

        _camera = Camera.main;

        if (_zones == null || _zones.Length == 0)
            Debug.LogWarning("[BackgroundSpawner] No zones configured. Add entries to the Zones array in the Inspector.", this);
        else
            Debug.Log($"[BackgroundSpawner] {_zones.Length} zone(s) configured. First zone needs {_zones[0].tileCount} tiles.", this);

        foreach (var def in _tileDefinitions)
            if (!def.isDefault && !def.isSeparator && !def.isEventTile)
                _wallsSince[def] = def.minWallsBefore;

        if (_clearExistingOnStart)
        {
            // Wipe stale pre-placed tiles/overlays so spawning (and challenges)
            // start from this object's position, near the player — not from the
            // right edge of leftover tiles far down the level.
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _spawnX = transform.position.x;
            _currentZoneStartX = _spawnX;
            _lastWasSeparator = true;
        }
        else if (!RebuildFromExistingTiles())
        {
            _spawnX = transform.position.x;
            _currentZoneStartX = _spawnX;
            _lastWasSeparator = true;
        }
    }

    private bool RebuildFromExistingTiles()
    {
        float maxRight = float.MinValue;
        bool foundAny = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            var tile = child.GetComponent<Tile>();
            var sr = child.GetComponent<SpriteRenderer>();

            if (tile != null && tile.Definition != null)
            {
                float rightEdge = child.transform.position.x - tile.Definition.PivotOffsetX + tile.Definition.Width;
                _active.Add((child, tile.Definition, rightEdge));
                if (rightEdge > maxRight) maxRight = rightEdge;
                foundAny = true;
            }
            else if (sr != null && child.name != "ZoneOverlay" && child.name != "SpecificZoneOverlay")
            {
                float rightEdge = sr.bounds.max.x;
                _manualTiles.Add((child, rightEdge));
                if (rightEdge > maxRight) maxRight = rightEdge;
                foundAny = true;
            }
        }

        if (!foundAny) return false;

        if (_active.Count > 0)
            _active.Sort((a, b) => a.rightEdge.CompareTo(b.rightEdge));

        _spawnX = maxRight;
        _currentZoneStartX = _spawnX;
        _currentZoneIndex = 0;
        _middleIndexThisZone = -1;
        _hasMidTile = false;
        _midTileDef = null;
        _lastWasSeparator = _active.Count > 0 ? _active[_active.Count - 1].def.isSeparator : true;
        return true;
    }

    private void Update()
    {
        if (_finished) return;
        SpawnAhead();
        RecycleBehind();
    }

    public void TriggerEnd() => _endTriggered = true;

    private void SpawnAhead()
    {
        float target = CameraRight() + _lookahead;
        while (!_finished && _spawnX < target)
            SpawnSingleTile();
    }

    private void SpawnSingleTile()
    {
        TileDefinition picked;
        bool placingMiddleTile = false;

        if (_lastWasSeparator)
        {
            if (_endTriggered)
            {
                picked = System.Array.Find(_tileDefinitions, d => d.isEnd);
                if (picked == null) { _finished = true; return; }
            }
            else
            {
                int contentIndex = _contentTilesInZone + 1;

                // Decide the middle tile for this zone when its first content tile appears.
                if (contentIndex == 1)
                    _middleIndexThisZone = HasActiveZone()
                        ? MiddleTileIndex(_zones[_currentZoneIndex].tileCount, _rng)
                        : -1;

                if (HasActiveZone() && contentIndex == _middleIndexThisZone)
                {
                    picked = MatchEventTile(_tileDefinitions, _zones[_currentZoneIndex].inputType, (float)_rng.NextDouble())
                             ?? PickWeighted();
                    placingMiddleTile = true;
                }
                else
                {
                    picked = PickWeighted();
                }
            }
        }
        else
        {
            picked = System.Array.Find(_tileDefinitions, d => d.isSeparator) ?? _tileDefinitions[0];
        }

        if (picked.Width <= 0f) return;

        var go = GetFromPool(picked);
        go.transform.position = new Vector3(_spawnX + picked.PivotOffsetX, _tileY, _tileZ);

        if (placingMiddleTile)
        {
            _midTileLeftX = _spawnX;
            _midTileWidth = picked.Width;
            _midTileDef = picked;
            _hasMidTile = true;
        }

        _spawnX += picked.Width;

        if (_lastWasSeparator)
        {
            UpdateGapCounters(picked, _tileDefinitions, _wallsSince);
            if (!picked.isEnd)
            {
                _contentTilesInZone++;
                CheckZone();
            }
        }

        _lastWasSeparator = !_lastWasSeparator;
        _active.Add((go, picked, _spawnX));

        if (picked.isEnd) _finished = true;
    }

    private bool HasActiveZone() =>
        _zones != null && _currentZoneIndex < _zones.Length && _zones[_currentZoneIndex].tileCount > 0;

    private TileDefinition PickWeighted()
    {
        float totalWeight = 0f;
        foreach (var def in _tileDefinitions)
            if (!def.isSeparator && !def.isEnd && !def.isEventTile &&
                (def.isDefault || (_wallsSince.TryGetValue(def, out int c) && c >= def.minWallsBefore)))
                totalWeight += def.weight;

        return PickTileType(_tileDefinitions, _wallsSince, (float)(_rng.NextDouble() * totalWeight));
    }

    private void CheckZone()
    {
        if (_zones == null || _currentZoneIndex >= _zones.Length) return;
        var zone = _zones[_currentZoneIndex];
        Debug.Log($"[BackgroundSpawner] Zone {_currentZoneIndex}: {_contentTilesInZone}/{zone.tileCount} tiles, startX={_currentZoneStartX:F2}, spawnX={_spawnX:F2}");
        if (_contentTilesInZone < zone.tileCount) return;

        CreateSectionOverlays(_currentZoneStartX, _spawnX, zone);

        _currentZoneIndex++;
        _currentZoneStartX = _spawnX;
        _contentTilesInZone = 0;
        _middleIndexThisZone = -1;
        _hasMidTile = false;
        _midTileDef = null;
    }

    private void CreateSectionOverlays(float startX, float endX, TileZone zone)
    {
        float width = endX - startX;
        if (width <= 0f) { Debug.LogWarning("[BackgroundSpawner] Section overlay skipped — width <= 0"); return; }

        float height = OverlayHeight();

        // Gray travel overlay across the whole section.
        CreateOverlayQuad("ZoneOverlay", startX, width, height, _defaultZoneColor, 1);

        // Colored overlay over just the specific (middle) challenge tile, on top.
        if (_hasMidTile && _midTileWidth > 0f)
        {
            Color overlayColor = _midTileDef != null ? _midTileDef.overlayColor : Color.white;
            CreateOverlayQuad("SpecificZoneOverlay", _midTileLeftX, _midTileWidth, height, overlayColor, 2);
            float midpointX = _midTileLeftX + _midTileWidth * 0.5f;
            _challenges.Add(new ChallengePoint(midpointX, zone.inputType));
            Debug.Log($"[BackgroundSpawner] Challenge at x={midpointX:F2} type={zone.inputType}");
        }
        else
        {
            Debug.LogWarning($"[BackgroundSpawner] No specific-zone tile recorded for zone {_currentZoneIndex}; no challenge created.", this);
        }
    }

    private float OverlayHeight()
    {
        float height = 0f;
        foreach (var def in _tileDefinitions)
            if (def.sprite != null && !def.isSeparator && !def.isEnd)
                height = Mathf.Max(height, def.sprite.bounds.size.y);
        return height <= 0f ? 1f : height;
    }

    private void CreateOverlayQuad(string name, float leftX, float width, float height, Color color, int sortingOrder)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        var obj = new GameObject(name);
        obj.transform.SetParent(transform);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        obj.transform.position = new Vector3(leftX, _tileY, _overlayZ);
        obj.transform.localScale = new Vector3(width, height, 1f);
    }

    private void RecycleBehind()
    {
        float cutoff = _camera.transform.position.x - _recycleBuffer;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].rightEdge < cutoff)
            {
                ReturnToPool(_active[i].go, _active[i].def);
                _active.RemoveAt(i);
            }
        }
        for (int i = _manualTiles.Count - 1; i >= 0; i--)
        {
            if (_manualTiles[i].rightEdge < cutoff)
            {
                Destroy(_manualTiles[i].go);
                _manualTiles.RemoveAt(i);
            }
        }
    }

    private GameObject GetFromPool(TileDefinition def)
    {
        if (_pool.TryGetValue(def, out var queue) && queue.Count > 0)
        {
            var go = queue.Dequeue();
            go.SetActive(true);
            return go;
        }

        var obj = new GameObject(def.name);
        obj.transform.SetParent(transform);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = def.sprite;
        var tile = obj.AddComponent<Tile>();
        tile.Definition = def;
        return obj;
    }

    private void ReturnToPool(GameObject go, TileDefinition def)
    {
        go.SetActive(false);
        if (!_pool.ContainsKey(def))
            _pool[def] = new Queue<GameObject>();
        _pool[def].Enqueue(go);
    }

    [ContextMenu("Populate Preview")]
    public void PopulatePreview()
    {
        if (_tileDefinitions == null || _tileDefinitions.Length == 0)
        {
            Debug.LogWarning("[BackgroundSpawner] No TileDefinitions assigned.", this);
            return;
        }

        ClearPreview();

        var cam = Camera.main;
        float right = cam != null ? cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x : 8f;

        foreach (var def in _tileDefinitions)
            if (!def.isDefault && !def.isSeparator && !def.isEventTile)
                _wallsSince[def] = def.minWallsBefore;

        _spawnX = transform.position.x;
        _currentZoneStartX = _spawnX;
        _lastWasSeparator = true;
        _endTriggered = false;
        _finished = false;
        _contentTilesInZone = 0;
        _currentZoneIndex = 0;
        _middleIndexThisZone = -1;
        _hasMidTile = false;
        _midTileDef = null;

        int safety = 500;
        while (!_finished && _spawnX < right + _lookahead && safety-- > 0)
            SpawnSingleTile();
    }

    [ContextMenu("Clear Preview")]
    public void ClearPreview()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        _active.Clear();
        _manualTiles.Clear();
        _challenges.Clear();
        foreach (var q in _pool.Values) q.Clear();
        _pool.Clear();
        _wallsSince.Clear();
        _spawnX = 0f;
        _lastWasSeparator = true;
        _endTriggered = false;
        _finished = false;
        _contentTilesInZone = 0;
        _currentZoneIndex = 0;
        _currentZoneStartX = 0f;
        _middleIndexThisZone = -1;
        _hasMidTile = false;
        _midTileDef = null;
    }

    private float CameraLeft() =>
        _camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;

    private float CameraRight() =>
        _camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;

    public static TileDefinition PickTileType(
        TileDefinition[] definitions,
        Dictionary<TileDefinition, int> wallsSince,
        float roll)
    {
        float cumulative = 0f;
        foreach (var def in definitions)
        {
            if (def.isSeparator || def.isEnd || def.isEventTile) continue;
            if (!def.isDefault && (!wallsSince.TryGetValue(def, out int count) || count < def.minWallsBefore))
                continue;

            cumulative += def.weight;
            if (roll < cumulative)
                return def;
        }

        foreach (var def in definitions)
            if (def.isDefault) return def;

        return definitions[0];
    }

    public static void UpdateGapCounters(
        TileDefinition placed,
        TileDefinition[] definitions,
        Dictionary<TileDefinition, int> wallsSince)
    {
        if (placed.isDefault)
        {
            foreach (var def in definitions)
                if (!def.isDefault)
                    wallsSince[def] = (wallsSince.TryGetValue(def, out int v) ? v : 0) + 1;
        }
        else
        {
            wallsSince[placed] = 0;
        }
    }

    /// <summary>
    /// The 1-based content-tile index that is the specific zone within a section.
    /// Odd counts use the exact middle; even counts randomly pick the lower or
    /// upper of the two central tiles. Returns -1 for non-positive counts.
    /// </summary>
    public static int MiddleTileIndex(int tileCount, System.Random rng)
    {
        if (tileCount <= 0) return -1;
        if ((tileCount & 1) == 1) return (tileCount + 1) / 2;

        int lower = tileCount / 2;
        return rng.Next(0, 2) == 0 ? lower : lower + 1;
    }

    /// <summary>
    /// Weighted-random pick among event tiles matching <paramref name="type"/>.
    /// <paramref name="roll01"/> is in [0, 1). Returns null when none match, so
    /// the caller can fall back to a normal tile.
    /// </summary>
    public static TileDefinition MatchEventTile(TileDefinition[] definitions, ZoneInputType type, float roll01)
    {
        float totalWeight = 0f;
        foreach (var def in definitions)
            if (def.isEventTile && def.eventType == type)
                totalWeight += def.weight;

        if (totalWeight <= 0f) return null;

        float target = roll01 * totalWeight;
        float cumulative = 0f;
        foreach (var def in definitions)
        {
            if (!def.isEventTile || def.eventType != type) continue;
            cumulative += def.weight;
            if (target < cumulative) return def;
        }

        // Floating-point edge: return the last matching tile.
        for (int i = definitions.Length - 1; i >= 0; i--)
            if (definitions[i].isEventTile && definitions[i].eventType == type)
                return definitions[i];

        return null;
    }
}
