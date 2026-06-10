# Background Tile Spawner — Design Spec
_Date: 2026-05-22_

## Overview

An endless-runner background system for MR KIM. The player moves right (driven by microphone amplitude) while the camera follows. Tiles spawn ahead of the camera and are recycled into a pool when they pass behind it. Special tile types (doors, decoratives) enforce a minimum number of wall tiles between consecutive appearances to prevent runs like `wall door door door`.

---

## Data Model

### `TileDefinition` (ScriptableObject)

One asset per tile type, created in the Unity editor.

| Field | Type | Description |
|---|---|---|
| `sprite` | `Sprite` | The tile's visual |
| `width` | `float` | World-unit width of this tile |
| `weight` | `float` | Relative spawn probability (e.g. wall=10, door=2) |
| `minWallsBefore` | `int` | Minimum wall tiles required before this type can spawn again. 0 for the wall tile itself, ≥1 for specials. |
| `isDefault` | `bool` | Marks this as the filler/wall tile — always eligible to spawn |

### `Tile` (MonoBehaviour)

Minimal component placed on every tile GameObject. Holds a reference back to its `TileDefinition` so the spawner knows which pool to return it to on recycle.

---

## BackgroundSpawner (MonoBehaviour)

Single MonoBehaviour on an empty scene GameObject. Owns both the spawn logic and the pool.

### Inspector Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `tileDefinitions` | `TileDefinition[]` | — | All tile types; drag SOs here |
| `lookahead` | `float` | `3` | World units ahead of camera right edge to keep filled |
| `recycleBuffer` | `float` | `1` | World units past camera left edge before tile is recycled |
| `tileY` | `float` | `0` | Y position for all tiles |

### Internal State

- `float _spawnX` — X position where the next tile's left edge will be placed; initialized to `camera.left` on `Start` so the screen fills immediately
- `Dictionary<TileDefinition, int> _wallsSince` — per-special-type counter: how many wall tiles have been placed since this type last appeared; initialized to `type.minWallsBefore` for each type so all specials are eligible from the very first tile
- `Dictionary<TileDefinition, Queue<GameObject>> _pool` — per-type object pool
- `List<(GameObject go, TileDefinition def, float rightEdge)> _active` — currently visible tiles

### Per-Frame Logic

**1. Spawn ahead**

While `_spawnX < camera.right + lookahead`:
1. Build eligible set: wall (`isDefault`) is always included; a special type is included only if `_wallsSince[type] >= type.minWallsBefore`
2. Weighted-random pick from eligible set (sum weights, roll 0–sum, walk list)
3. Pull tile from pool (dequeue if available, else instantiate)
4. Position tile at `(_spawnX + picked.width / 2, tileY, 0)`
5. Advance `_spawnX += picked.width`
6. Update gap counters:
   - If picked tile `isDefault`: increment `_wallsSince[t]` for every non-default type
   - Else: set `_wallsSince[picked] = 0`
7. Add to `_active` list

**2. Recycle behind**

Iterate `_active` (back-to-front to allow safe removal):
- If `tile.rightEdge < camera.left - recycleBuffer`: disable GameObject, enqueue to `_pool[def]`, remove from `_active`

---

## Pool Management

Lives inside `BackgroundSpawner` as a `Dictionary<TileDefinition, Queue<GameObject>>`.

**Get(TileDefinition def)**
- If `_pool[def]` has items: dequeue, re-enable, return
- Else: instantiate new GameObject with `SpriteRenderer` + `Tile` component; set sprite, localScale.x from `def.width`; return

**Return(GameObject go, TileDefinition def)**
- Disable GameObject
- Enqueue to `_pool[def]`

Pool starts empty (lazy instantiation). Tiles are created on demand on first pass and reused indefinitely.

---

## Files to Create

| File | Type | Purpose |
|---|---|---|
| `Assets/Scripts/TileDefinition.cs` | ScriptableObject | Data container per tile type |
| `Assets/Scripts/Tile.cs` | MonoBehaviour | Back-reference on tile GameObjects |
| `Assets/Scripts/BackgroundSpawner.cs` | MonoBehaviour | Spawn, gap enforcement, pool, recycle |

---

## Out of Scope

- Vertical variation (all tiles share a single Y)
- Tile animations or state changes after spawn
- Save/seed-based level generation
