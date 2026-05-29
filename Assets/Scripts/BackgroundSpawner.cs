using System.Collections.Generic;
using UnityEngine;

public enum ZoneInputType
{
    Murmur,  // low sustained amplitude
    Talk,    // normal speaking amplitude
    Scream,  // high sustained amplitude
    Clap     // sharp transient peak
}

/// <summary>How many events of a given type appear in a run (count >= 1).</summary>
[System.Serializable]
public struct EventSpec
{
    public ZoneInputType type;
    public int count;
}

public enum LevelSlotKind { Filler, Event, Finish }

/// <summary>One content-tile position in the generated level plan.</summary>
public readonly struct LevelSlot
{
    public readonly LevelSlotKind kind;
    public readonly ZoneInputType eventType; // meaningful only when kind == Event

    private LevelSlot(LevelSlotKind kind, ZoneInputType eventType)
    {
        this.kind = kind;
        this.eventType = eventType;
    }

    public static LevelSlot Filler() => new LevelSlot(LevelSlotKind.Filler, default);
    public static LevelSlot Event(ZoneInputType type) => new LevelSlot(LevelSlotKind.Event, type);
    public static LevelSlot Finish() => new LevelSlot(LevelSlotKind.Finish, default);
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
    [SerializeField] private float _overlayZ = -0.5f;
    [Tooltip("Color painted over the gray travel stretches (the filler between events).")]
    [SerializeField] private Color _defaultZoneColor = Color.gray;

    [Header("Procedural generation")]
    [Tooltip("How many events of each type appear in a run (each count >= 1). Order is shuffled per run.")]
    [SerializeField] private EventSpec[] _eventCounts;
    [Tooltip("Filler tiles before the first event (inclusive range).")]
    [SerializeField] private int _minStartTiles = 2;
    [SerializeField] private int _maxStartTiles = 3;
    [Tooltip("Filler tiles between consecutive events (inclusive range).")]
    [SerializeField] private int _minBetweenTiles = 1;
    [SerializeField] private int _maxBetweenTiles = 2;
    [Tooltip("Verbose generation logging: the built plan, and each tile as it is placed.")]
    [SerializeField] private bool _debugGeneration = true;

    private float _spawnX;
    private bool _finished;
    private Camera _camera;

    // The generated level: an ordered list of content-tile slots. Separators are
    // interleaved automatically between them.
    private readonly List<LevelSlot> _plan = new();
    private int _planIndex;

    // Open gray (travel) overlay stretch covering the current run of filler tiles.
    private bool _grayOpen;
    private float _grayStartX;

    // Finish (end tile) midpoint; the run ends when the player reaches it.
    private bool _hasFinish;
    private float _finishX;

    private System.Random _rng = new();
    // Counts default tiles placed since each special type last appeared.
    private readonly Dictionary<TileDefinition, int> _wallsSince = new();
    // Last sprite-variant index used for each definition, so the next placement
    // of that tile can pick a different one.
    private readonly Dictionary<TileDefinition, int> _lastSpriteIndex = new();
    private readonly Dictionary<TileDefinition, Queue<GameObject>> _pool = new();
    private readonly List<(GameObject go, TileDefinition def, float rightEdge)> _active = new();
    private readonly List<ChallengePoint> _challenges = new();

    /// <summary>Challenge points discovered so far, ordered left to right.</summary>
    public IReadOnlyList<ChallengePoint> Challenges => _challenges;

    /// <summary>True once the finish (end) tile has been placed.</summary>
    public bool HasFinish => _hasFinish;

    /// <summary>World x of the finish tile's midpoint; reaching it ends the run.</summary>
    public float FinishX => _finishX;

    private void Start()
    {
        if (_tileDefinitions == null || _tileDefinitions.Length == 0)
        {
            Debug.LogError("[BackgroundSpawner] _tileDefinitions is empty. Assign TileDefinition assets in the Inspector.", this);
            enabled = false;
            return;
        }

        _camera = Camera.main;

        // Always generate fresh: wipe any pre-placed children so spawning (and
        // challenges) start from this object's position, near the player.
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _spawnX = transform.position.x;
        InitGeneration();

        Debug.Log($"[BackgroundSpawner] Plan built: {_plan.Count} slots, {CountEventSlots()} events.", this);
    }

    private int CountEventSlots()
    {
        int n = 0;
        foreach (var s in _plan) if (s.kind == LevelSlotKind.Event) n++;
        return n;
    }

    /// <summary>Build the level plan and reset per-run generation state.</summary>
    private void InitGeneration()
    {
        _wallsSince.Clear();
        _lastSpriteIndex.Clear();
        foreach (var def in _tileDefinitions)
            if (def != null && !def.isDefault && !def.isSeparator && !def.isEventTile)
                _wallsSince[def] = def.minWallsBefore;

        _challenges.Clear();
        _plan.Clear();
        _plan.AddRange(BuildLevelPlan(_eventCounts, _minStartTiles, _maxStartTiles,
                                      _minBetweenTiles, _maxBetweenTiles, _rng));
        _planIndex = 0;
        _grayOpen = false;
        _grayStartX = _spawnX;
        _hasFinish = false;
        _finishX = 0f;
        _finished = false;

        if (_debugGeneration) LogPlanSummary();
    }

    /// <summary>Logs the built plan: start-filler count, each event with the
    /// number of filler tiles that follow it, and the finish.</summary>
    private void LogPlanSummary()
    {
        var sb = new System.Text.StringBuilder("[BackgroundSpawner] PLAN  ");

        int i = 0;
        int startFiller = 0;
        while (i < _plan.Count && _plan[i].kind == LevelSlotKind.Filler) { startFiller++; i++; }
        sb.Append($"startFiller={startFiller}");

        int eventNum = 0;
        while (i < _plan.Count)
        {
            if (_plan[i].kind == LevelSlotKind.Event)
            {
                int between = 0;
                int j = i + 1;
                while (j < _plan.Count && _plan[j].kind == LevelSlotKind.Filler) { between++; j++; }
                sb.Append($" | E{eventNum}={_plan[i].eventType}(+{between})");
                eventNum++;
                i = j;
            }
            else if (_plan[i].kind == LevelSlotKind.Finish)
            {
                sb.Append(" | FINISH");
                i++;
            }
            else { i++; }
        }

        sb.Append($"   (events={eventNum}, slots={_plan.Count}; ranges start={_minStartTiles}-{_maxStartTiles}, between={_minBetweenTiles}-{_maxBetweenTiles})");
        Debug.Log(sb.ToString(), this);
    }

    private void Update()
    {
        if (_finished) return;
        SpawnAhead();
        RecycleBehind();
    }

    private void SpawnAhead()
    {
        float target = CameraRight() + _lookahead;
        while (!_finished && _spawnX < target)
            SpawnSingleTile();
    }

    private void SpawnSingleTile()
    {
        // One placed tile per plan slot — no separators.
        if (_planIndex >= _plan.Count)
        {
            CloseGrayStretch();
            _finished = true;
            return;
        }

        var slot = _plan[_planIndex];
        _planIndex++;

        switch (slot.kind)
        {
            case LevelSlotKind.Finish:
            {
                CloseGrayStretch();
                var endDef = System.Array.Find(_tileDefinitions, d => d != null && d.isEnd);
                if (endDef == null || endDef.Width <= 0f)
                {
                    Debug.LogWarning("[BackgroundSpawner] No end tile defined; level has no finish point.", this);
                    _finished = true;
                    return;
                }
                float leftX = _spawnX;
                float placedWidth = PlaceTile(endDef, "FINISH");
                _finishX = leftX + placedWidth * 0.5f;
                _hasFinish = true;
                _finished = true;
                Debug.Log($"[BackgroundSpawner] Finish at x={_finishX:F2}", this);
                return;
            }

            case LevelSlotKind.Event:
            {
                CloseGrayStretch();
                var picked = MatchEventTile(_tileDefinitions, slot.eventType, (float)_rng.NextDouble());
                if (picked == null)
                {
                    Debug.LogWarning($"[BackgroundSpawner] No event tile for {slot.eventType} — add a TileDefinition with isEventTile=true and eventType={slot.eventType} to _tileDefinitions. Falling back to a filler tile (it'll look like a wall).", this);
                    picked = PickWeighted();
                }
                if (picked.Width <= 0f) { _finished = true; return; }
                float leftX = _spawnX;
                float placedWidth = PlaceTile(picked, $"slot#{_planIndex - 1} EVENT {slot.eventType}");
                CreateEventOverlayAndChallenge(leftX, placedWidth, picked, slot.eventType);
                return;
            }

            default: // Filler
            {
                if (!_grayOpen) { _grayOpen = true; _grayStartX = _spawnX; }
                var filler = PickWeighted();
                if (filler.Width <= 0f) { _finished = true; return; }
                UpdateGapCounters(filler, _tileDefinitions, _wallsSince);
                PlaceTile(filler, $"slot#{_planIndex - 1} FILLER");
                return;
            }
        }
    }

    /// <summary>
    /// Places a tile at the current frontier. Chooses a sprite variant (different
    /// from the last used for this def) and sizes the advance/position to THAT
    /// sprite's bounds, so variants of differing widths still tile seamlessly with
    /// no gaps or overlaps. Returns the placed width.
    /// </summary>
    private float PlaceTile(TileDefinition def, string label)
    {
        int last = _lastSpriteIndex.TryGetValue(def, out int li) ? li : -1;
        int index = PickSpriteIndex(def.SpriteCount, last, _rng);
        _lastSpriteIndex[def] = index;
        Sprite sprite = def.GetSprite(index);

        float width = sprite != null ? sprite.bounds.size.x : def.Width;
        float pivotOffset = sprite != null ? -sprite.bounds.min.x : def.PivotOffsetX;
        float leftX = _spawnX;

        var go = GetFromPool(def);
        go.GetComponent<SpriteRenderer>().sprite = sprite;
        go.transform.position = new Vector3(leftX + pivotOffset, _tileY, _tileZ);
        _spawnX += width;
        _active.Add((go, def, _spawnX));

        if (_debugGeneration)
            Debug.Log($"[BackgroundSpawner] {label} '{def.name}' sprite='{(sprite != null ? sprite.name : "null")}' " +
                      $"left={leftX:F2} pos.x={go.transform.position.x:F2} w={width:F2} right={_spawnX:F2} " +
                      $"(scale={go.transform.lossyScale.x:F2})", go);

        return width;
    }

    private void CloseGrayStretch()
    {
        if (!_grayOpen) return;
        float width = _spawnX - _grayStartX;
        // Overlays are a debug visualization only — skip in release builds.
        if (width > 0f && Debug.isDebugBuild)
            CreateOverlayQuad("ZoneOverlay", _grayStartX, width, OverlayHeight(), _defaultZoneColor, 1);
        if (_debugGeneration)
            Debug.Log($"[BackgroundSpawner] gray stretch x={_grayStartX:F2}..{_spawnX:F2} (w={width:F2})", this);
        _grayOpen = false;
    }

    private void CreateEventOverlayAndChallenge(float leftX, float width, TileDefinition def, ZoneInputType type)
    {
        // The colored overlay is a debug visualization only — skip in release
        // builds. The ChallengePoint (gameplay) is always created.
        if (Debug.isDebugBuild)
        {
            Color color = def != null ? def.overlayColor : Color.white;
            CreateOverlayQuad("SpecificZoneOverlay", leftX, width, OverlayHeight(), color, 2);
        }
        float midpointX = leftX + width * 0.5f;
        _challenges.Add(new ChallengePoint(midpointX, type));
        Debug.Log($"[BackgroundSpawner] Challenge at x={midpointX:F2} type={type}", this);
    }

    private TileDefinition PickWeighted()
    {
        float totalWeight = 0f;
        foreach (var def in _tileDefinitions)
            if (!def.isSeparator && !def.isEnd && !def.isEventTile &&
                (def.isDefault || (_wallsSince.TryGetValue(def, out int c) && c >= def.minWallsBefore)))
                totalWeight += def.weight;

        return PickTileType(_tileDefinitions, _wallsSince, (float)(_rng.NextDouble() * totalWeight));
    }

    private float OverlayHeight()
    {
        float height = 0f;
        foreach (var def in _tileDefinitions)
            if (def.PrimarySprite != null && !def.isSeparator && !def.isEnd)
                height = Mathf.Max(height, def.PrimarySprite.bounds.size.y);
        return height <= 0f ? 1f : height;
    }

    private void CreateOverlayQuad(string objectName, float leftX, float width, float height, Color color, int sortingOrder)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        var obj = new GameObject(objectName);
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
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<Tile>().Definition = def;
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

        _spawnX = transform.position.x;
        InitGeneration();

        int safety = 1000;
        while (!_finished && _spawnX < right + _lookahead && safety-- > 0)
            SpawnSingleTile();
    }

    [ContextMenu("Clear Preview")]
    public void ClearPreview()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        _active.Clear();
        _challenges.Clear();
        _plan.Clear();
        foreach (var q in _pool.Values) q.Clear();
        _pool.Clear();
        _wallsSince.Clear();
        _lastSpriteIndex.Clear();
        _spawnX = 0f;
        _planIndex = 0;
        _grayOpen = false;
        _grayStartX = 0f;
        _hasFinish = false;
        _finishX = 0f;
        _finished = false;
    }

    private float CameraRight() =>
        _camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;

    /// <summary>
    /// Build the ordered level plan: start filler (before the first event only),
    /// then the shuffled event pool (each type repeated by its count) where every
    /// event is followed by a between-filler stretch — including the last event,
    /// so there is a between stretch before the finish slot.
    /// </summary>
    public static List<LevelSlot> BuildLevelPlan(
        IReadOnlyList<EventSpec> eventSpecs,
        int minStartTiles, int maxStartTiles,
        int minBetweenTiles, int maxBetweenTiles,
        System.Random rng)
    {
        var plan = new List<LevelSlot>();

        var events = new List<ZoneInputType>();
        if (eventSpecs != null)
            foreach (var spec in eventSpecs)
                for (int i = 0; i < spec.count; i++)
                    events.Add(spec.type);
        Shuffle(events, rng);

        int start = RandomRange(minStartTiles, maxStartTiles, rng);
        for (int i = 0; i < start; i++) plan.Add(LevelSlot.Filler());

        foreach (var type in events)
        {
            plan.Add(LevelSlot.Event(type));
            int between = RandomRange(minBetweenTiles, maxBetweenTiles, rng);
            for (int i = 0; i < between; i++) plan.Add(LevelSlot.Filler());
        }

        plan.Add(LevelSlot.Finish());
        return plan;
    }

    private static int RandomRange(int min, int max, System.Random rng)
    {
        if (min < 0) min = 0;
        if (max < min) max = min;
        return rng.Next(min, max + 1);
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Random sprite-variant index in [0, count) that differs from
    /// <paramref name="lastIndex"/>. Returns 0 for a single sprite, and a uniform
    /// pick when there is no valid last index.
    /// </summary>
    public static int PickSpriteIndex(int count, int lastIndex, System.Random rng)
    {
        if (count <= 1) return 0;
        if (lastIndex < 0 || lastIndex >= count) return rng.Next(0, count);

        int r = rng.Next(0, count - 1); // pick among the other (count-1) indices
        return r < lastIndex ? r : r + 1;
    }

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
