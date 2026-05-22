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
    public Color color;
    public ZoneInputType inputType;
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

    private float _spawnX;
    private bool _lastWasSeparator = true;
    private bool _endTriggered;
    private bool _finished;
    private Camera _camera;
    private int _contentTilesInZone;
    private int _currentZoneIndex;
    private float _currentZoneStartX;
    // Counts default tiles placed since each special type last appeared.
    private readonly Dictionary<TileDefinition, int> _wallsSince = new();
    private readonly Dictionary<TileDefinition, Queue<GameObject>> _pool = new();
    private readonly List<(GameObject go, TileDefinition def, float rightEdge)> _active = new();
    private readonly List<(GameObject go, float rightEdge)> _manualTiles = new();
    private readonly List<(float startX, float endX, ZoneInputType inputType)> _zoneBoundaries = new();

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
            if (!def.isDefault && !def.isSeparator)
                _wallsSince[def] = def.minWallsBefore;

        if (!RebuildFromExistingTiles())
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
            else if (sr != null && child.name != "ZoneOverlay")
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
        if (_lastWasSeparator)
        {
            if (_endTriggered)
            {
                picked = System.Array.Find(_tileDefinitions, d => d.isEnd);
                if (picked == null) { _finished = true; return; }
            }
            else
            {
                float totalWeight = 0f;
                foreach (var def in _tileDefinitions)
                    if (!def.isSeparator && !def.isEnd && (def.isDefault || (_wallsSince.TryGetValue(def, out int c) && c >= def.minWallsBefore)))
                        totalWeight += def.weight;

                picked = PickTileType(_tileDefinitions, _wallsSince, Random.Range(0f, totalWeight));
            }
        }
        else
        {
            picked = System.Array.Find(_tileDefinitions, d => d.isSeparator) ?? _tileDefinitions[0];
        }

        if (picked.Width <= 0f) return;

        var go = GetFromPool(picked);
        go.transform.position = new Vector3(_spawnX + picked.PivotOffsetX, _tileY, _tileZ);
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

    private void CheckZone()
    {
        if (_zones == null || _currentZoneIndex >= _zones.Length) return;
        var zone = _zones[_currentZoneIndex];
        Debug.Log($"[BackgroundSpawner] Zone {_currentZoneIndex}: {_contentTilesInZone}/{zone.tileCount} tiles, startX={_currentZoneStartX:F2}, spawnX={_spawnX:F2}");
        if (_contentTilesInZone < zone.tileCount) return;

        CreateZoneOverlay(_currentZoneStartX, _spawnX, zone.color, zone.inputType);
        _currentZoneIndex++;
        _currentZoneStartX = _spawnX;
        _contentTilesInZone = 0;
    }

    public ZoneInputType GetInputTypeAt(float x)
    {
        foreach (var (startX, endX, inputType) in _zoneBoundaries)
            if (x >= startX && x < endX)
                return inputType;
        return ZoneInputType.Talk;
    }

    private void CreateZoneOverlay(float startX, float endX, Color color, ZoneInputType inputType)
    {
        _zoneBoundaries.Add((startX, endX, inputType));
        float width = endX - startX;
        Debug.Log($"[BackgroundSpawner] Creating ZoneOverlay: x={startX:F2}→{endX:F2} width={width:F2} color={color} alpha={color.a:F2} type={inputType}");
        if (width <= 0f) { Debug.LogWarning("[BackgroundSpawner] ZoneOverlay skipped — width <= 0"); return; }

        float height = 0f;
        foreach (var def in _tileDefinitions)
            if (def.sprite != null && !def.isSeparator && !def.isEnd)
                height = Mathf.Max(height, def.sprite.bounds.size.y);
        if (height <= 0f) height = 1f;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var overlaySprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        var obj = new GameObject("ZoneOverlay");
        obj.transform.SetParent(transform);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = overlaySprite;
        sr.color = color;
        sr.sortingOrder = 1;
        obj.transform.position = new Vector3(startX, _tileY, _overlayZ);
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
            if (!def.isDefault && !def.isSeparator)
                _wallsSince[def] = def.minWallsBefore;

        _spawnX = transform.position.x;
        _currentZoneStartX = _spawnX;
        _lastWasSeparator = true;
        _endTriggered = false;
        _finished = false;
        _contentTilesInZone = 0;
        _currentZoneIndex = 0;

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
        _zoneBoundaries.Clear();
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
            if (def.isSeparator || def.isEnd) continue;
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
}
