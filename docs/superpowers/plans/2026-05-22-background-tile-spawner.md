# Background Tile Spawner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a camera-following endless-runner tile spawner that places wall/door/decorative sprites ahead of the player, enforces per-type minimum-gap rules, and recycles tiles via an object pool.

**Architecture:** `TileDefinition` ScriptableObjects hold per-type config (sprite, width, weight, min gap). `BackgroundSpawner` owns spawn-ahead, recycle-behind, gap counters, and a per-type `Queue<GameObject>` pool. Two pure `public static` helpers (`PickTileType`, `UpdateGapCounters`) are tested in Edit Mode without needing a scene.

**Tech Stack:** Unity 2D (URP), C#, Unity Test Framework (NUnit Edit Mode tests)

---

## File Map

| Path | Action | Purpose |
|---|---|---|
| `Assets/Scripts/TileDefinition.cs` | Create | ScriptableObject data container per tile type |
| `Assets/Scripts/Tile.cs` | Create | MonoBehaviour back-reference on tile GameObjects |
| `Assets/Scripts/Scripts.asmdef` | Create | Named assembly so tests can reference game code |
| `Assets/Scripts/BackgroundSpawner.cs` | Create | Spawn, gap enforcement, pool, recycle |
| `Assets/Tests/EditMode/MRKim.Tests.EditMode.asmdef` | Create | Test assembly definition |
| `Assets/Tests/EditMode/BackgroundSpawnerTests.cs` | Create | NUnit Edit Mode tests for pure logic |

---

## Task 1: TileDefinition ScriptableObject

**Files:**
- Create: `Assets/Scripts/TileDefinition.cs`

- [ ] **Create `Assets/Scripts/TileDefinition.cs`:**

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "TileDefinition", menuName = "MR KIM/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public Sprite sprite;
    public float width = 1f;
    public float weight = 1f;
    public int minWallsBefore = 0;
    public bool isDefault = false;
}
```

- [ ] **Commit:**
```
git add Assets/Scripts/TileDefinition.cs
git commit -m "feat: add TileDefinition ScriptableObject"
```

---

## Task 2: Tile MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Tile.cs`

- [ ] **Create `Assets/Scripts/Tile.cs`:**

```csharp
using UnityEngine;

public class Tile : MonoBehaviour
{
    public TileDefinition Definition { get; set; }
}
```

- [ ] **Commit:**
```
git add Assets/Scripts/Tile.cs
git commit -m "feat: add Tile MonoBehaviour"
```

---

## Task 3: Assembly definitions for testability

**Files:**
- Create: `Assets/Scripts/Scripts.asmdef`
- Create: `Assets/Tests/EditMode/MRKim.Tests.EditMode.asmdef`

Unity's default `Assembly-CSharp` cannot be referenced by name in an `.asmdef`. Giving the game scripts their own named assembly lets the test assembly reference them explicitly.

- [ ] **Create `Assets/Scripts/Scripts.asmdef`:**

```json
{
    "name": "MRKim.Scripts",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Create directory `Assets/Tests/EditMode/`** (just create a placeholder by creating the asmdef file there)

- [ ] **Create `Assets/Tests/EditMode/MRKim.Tests.EditMode.asmdef`:**

```json
{
    "name": "MRKim.Tests.EditMode",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MRKim.Scripts"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Let Unity recompile** — no errors expected since no scripts reference cross-assembly types yet.

- [ ] **Commit:**
```
git add Assets/Scripts/Scripts.asmdef Assets/Tests/
git commit -m "chore: add assembly definitions for Edit Mode testability"
```

---

## Task 4: PickTileType — TDD

**Files:**
- Create: `Assets/Scripts/BackgroundSpawner.cs` (shell + `PickTileType`)
- Create: `Assets/Tests/EditMode/BackgroundSpawnerTests.cs`

- [ ] **Create `Assets/Tests/EditMode/BackgroundSpawnerTests.cs` with the failing tests:**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BackgroundSpawnerTests
{
    private static TileDefinition MakeDef(float weight, int minWalls, bool isDefault = false)
    {
        var def = ScriptableObject.CreateInstance<TileDefinition>();
        def.weight = weight;
        def.minWallsBefore = minWalls;
        def.isDefault = isDefault;
        return def;
    }

    [Test]
    public void PickTileType_WallAlwaysEligible()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        // door not eligible (wallsSince=0, needs 2); only wall in pool weight=10
        // roll=5 lands on wall
        var result = BackgroundSpawner.PickTileType(
            new[] { wall, door },
            new Dictionary<TileDefinition, int> { { door, 0 } },
            5f);

        Assert.AreEqual(wall, result);
    }

    [Test]
    public void PickTileType_SpecialNotPickedBelowMinGap()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        // door needs 2 walls, has 1 — not eligible; eligible total=10
        // roll=9.9 still lands on wall
        var result = BackgroundSpawner.PickTileType(
            new[] { wall, door },
            new Dictionary<TileDefinition, int> { { door, 1 } },
            9.9f);

        Assert.AreEqual(wall, result);
    }

    [Test]
    public void PickTileType_SpecialPickedWhenGapMet()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        // door eligible (wallsSince=2 >= minWalls=2); total=15
        // roll=12 > wall's range [0,10), lands on door [10,15)
        var result = BackgroundSpawner.PickTileType(
            new[] { wall, door },
            new Dictionary<TileDefinition, int> { { door, 2 } },
            12f);

        Assert.AreEqual(door, result);
    }
}
```

- [ ] **Open Unity Test Runner** (Window → General → Test Runner → EditMode). The 3 tests should appear but fail to compile because `BackgroundSpawner` doesn't exist yet — that's expected.

- [ ] **Create `Assets/Scripts/BackgroundSpawner.cs`** with just the shell and `PickTileType`:

```csharp
using System.Collections.Generic;
using UnityEngine;

public class BackgroundSpawner : MonoBehaviour
{
    [SerializeField] private TileDefinition[] _tileDefinitions;
    [SerializeField] private float _lookahead = 3f;
    [SerializeField] private float _recycleBuffer = 1f;
    [SerializeField] private float _tileY = 0f;

    private float _spawnX;
    private readonly Dictionary<TileDefinition, int> _wallsSince = new();
    private readonly Dictionary<TileDefinition, Queue<GameObject>> _pool = new();
    private readonly List<(GameObject go, TileDefinition def, float rightEdge)> _active = new();

    // roll: value in [0, sum-of-eligible-weights). Injected so callers control randomness.
    public static TileDefinition PickTileType(
        TileDefinition[] definitions,
        Dictionary<TileDefinition, int> wallsSince,
        float roll)
    {
        float cumulative = 0f;
        foreach (var def in definitions)
        {
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
}
```

- [ ] **Run the 3 PickTileType tests in the Test Runner.** Expected: all 3 PASS.

- [ ] **Commit:**
```
git add Assets/Scripts/BackgroundSpawner.cs Assets/Tests/EditMode/BackgroundSpawnerTests.cs
git commit -m "feat: add PickTileType with Edit Mode tests"
```

---

## Task 5: UpdateGapCounters — TDD

**Files:**
- Modify: `Assets/Tests/EditMode/BackgroundSpawnerTests.cs` (add 3 tests)
- Modify: `Assets/Scripts/BackgroundSpawner.cs` (add `UpdateGapCounters`)

- [ ] **Add 3 tests to `BackgroundSpawnerTests.cs`**, inside the class after the existing tests:

```csharp
    [Test]
    public void UpdateGapCounters_WallIncrementsAllSpecials()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        var decor = MakeDef(3f, 3);
        var wallsSince = new Dictionary<TileDefinition, int> { { door, 0 }, { decor, 1 } };

        BackgroundSpawner.UpdateGapCounters(wall, new[] { wall, door, decor }, wallsSince);

        Assert.AreEqual(1, wallsSince[door]);
        Assert.AreEqual(2, wallsSince[decor]);
    }

    [Test]
    public void UpdateGapCounters_SpecialResetsItsOwnCounter()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        var wallsSince = new Dictionary<TileDefinition, int> { { door, 5 } };

        BackgroundSpawner.UpdateGapCounters(door, new[] { wall, door }, wallsSince);

        Assert.AreEqual(0, wallsSince[door]);
    }

    [Test]
    public void UpdateGapCounters_SpecialDoesNotIncrementOtherSpecials()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var door = MakeDef(5f, 2);
        var decor = MakeDef(3f, 3);
        var wallsSince = new Dictionary<TileDefinition, int> { { door, 2 }, { decor, 1 } };

        BackgroundSpawner.UpdateGapCounters(door, new[] { wall, door, decor }, wallsSince);

        Assert.AreEqual(0, wallsSince[door]);
        Assert.AreEqual(1, wallsSince[decor]); // unchanged
    }
```

- [ ] **Run the Test Runner.** Expected: 3 new tests FAIL (method not defined yet), 3 previous tests still PASS.

- [ ] **Add `UpdateGapCounters` to `BackgroundSpawner.cs`**, inside the class after `PickTileType`:

```csharp
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
```

- [ ] **Run all 6 tests.** Expected: all 6 PASS.

- [ ] **Commit:**
```
git add Assets/Scripts/BackgroundSpawner.cs Assets/Tests/EditMode/BackgroundSpawnerTests.cs
git commit -m "feat: add UpdateGapCounters with tests"
```

---

## Task 6: BackgroundSpawner — pool, spawn, recycle

**Files:**
- Modify: `Assets/Scripts/BackgroundSpawner.cs` (add `Start`, `Update`, pool methods)

- [ ] **Replace `Assets/Scripts/BackgroundSpawner.cs`** with the complete implementation (keep `PickTileType` and `UpdateGapCounters` unchanged):

```csharp
using System.Collections.Generic;
using UnityEngine;

public class BackgroundSpawner : MonoBehaviour
{
    [SerializeField] private TileDefinition[] _tileDefinitions;
    [SerializeField] private float _lookahead = 3f;
    [SerializeField] private float _recycleBuffer = 1f;
    [SerializeField] private float _tileY = 0f;

    private float _spawnX;
    private readonly Dictionary<TileDefinition, int> _wallsSince = new();
    private readonly Dictionary<TileDefinition, Queue<GameObject>> _pool = new();
    private readonly List<(GameObject go, TileDefinition def, float rightEdge)> _active = new();

    private void Start()
    {
        foreach (var def in _tileDefinitions)
            if (!def.isDefault)
                _wallsSince[def] = def.minWallsBefore;

        _spawnX = CameraLeft();
    }

    private void Update()
    {
        SpawnAhead();
        RecycleBehind();
    }

    private void SpawnAhead()
    {
        float target = CameraRight() + _lookahead;
        while (_spawnX < target)
        {
            float totalWeight = 0f;
            foreach (var def in _tileDefinitions)
                if (def.isDefault || (_wallsSince.TryGetValue(def, out int c) && c >= def.minWallsBefore))
                    totalWeight += def.weight;

            var picked = PickTileType(_tileDefinitions, _wallsSince, Random.Range(0f, totalWeight));
            var go = GetFromPool(picked);
            go.transform.position = new Vector3(_spawnX + picked.width * 0.5f, _tileY, 0f);
            _spawnX += picked.width;

            UpdateGapCounters(picked, _tileDefinitions, _wallsSince);
            _active.Add((go, picked, _spawnX));
        }
    }

    private void RecycleBehind()
    {
        float cutoff = CameraLeft() - _recycleBuffer;
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
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = def.sprite;

        float nativeWidth = (def.sprite != null) ? def.sprite.bounds.size.x : 1f;
        float scale = (nativeWidth > 0f) ? def.width / nativeWidth : def.width;
        obj.transform.localScale = new Vector3(scale, scale, 1f);

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

    private static float CameraLeft() =>
        Camera.main.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;

    private static float CameraRight() =>
        Camera.main.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;

    public static TileDefinition PickTileType(
        TileDefinition[] definitions,
        Dictionary<TileDefinition, int> wallsSince,
        float roll)
    {
        float cumulative = 0f;
        foreach (var def in definitions)
        {
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
```

- [ ] **Run all 6 tests in Test Runner.** Expected: all 6 still PASS (no regressions).

- [ ] **Commit:**
```
git add Assets/Scripts/BackgroundSpawner.cs
git commit -m "feat: complete BackgroundSpawner with pool, spawn, and recycle"
```

---

## Task 7: Create TileDefinition assets and wire up scene

- [ ] **Create Wall TileDefinition asset:**
  In the Project window, right-click → Create → MR KIM → Tile Definition.
  Name it `WallTile`. Set: `isDefault = true`, `weight = 10`, `minWallsBefore = 0`. Assign the wall sprite.

- [ ] **Create Door TileDefinition asset:**
  Right-click → Create → MR KIM → Tile Definition.
  Name it `DoorTile`. Set: `isDefault = false`, `weight = 2`, `minWallsBefore = 2` (at least 2 walls between doors). Assign the door sprite. Set `width` to match the door sprite's intended world-unit width.

- [ ] **Create BackgroundSpawner GameObject in the scene:**
  In the Hierarchy, right-click → Create Empty. Name it `BackgroundSpawner`. Add the `BackgroundSpawner` component.

- [ ] **Wire up the inspector:**
  - Set `Tile Definitions` array size to 2
  - Assign `WallTile` to index 0, `DoorTile` to index 1
  - `Lookahead`: start with `5` (adjust if tiles pop in too late)
  - `Recycle Buffer`: `1`
  - `Tile Y`: match whatever Y your background should sit at (e.g. `0`)

- [ ] **Enter Play Mode.** Verify:
  - Tiles appear and cover the screen horizontally from the start
  - Player moves right (make noise), tiles scroll past as the camera follows
  - No console errors
  - No two door tiles appear back-to-back without at least 2 wall tiles between them
  - Old tiles disappear behind the camera (no infinite tile accumulation in the Hierarchy)

- [ ] **Commit:**
```
git add Assets/
git commit -m "feat: add TileDefinition assets and BackgroundSpawner scene setup"
```
