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

    private static TileDefinition MakeEventDef(float weight, ZoneInputType type)
    {
        var def = ScriptableObject.CreateInstance<TileDefinition>();
        def.weight = weight;
        def.isEventTile = true;
        def.eventType = type;
        return def;
    }

    [Test]
    public void MiddleTileIndex_OddCountIsExactMiddle()
    {
        Assert.AreEqual(3, BackgroundSpawner.MiddleTileIndex(5, new System.Random(1)));
        Assert.AreEqual(1, BackgroundSpawner.MiddleTileIndex(1, new System.Random(1)));
    }

    [Test]
    public void MiddleTileIndex_EvenCountIsOneOfTwoCentral()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            int result = BackgroundSpawner.MiddleTileIndex(6, new System.Random(seed));
            Assert.IsTrue(result == 3 || result == 4, $"seed {seed} gave {result}");
        }
    }

    [Test]
    public void MiddleTileIndex_NonPositiveReturnsMinusOne()
    {
        Assert.AreEqual(-1, BackgroundSpawner.MiddleTileIndex(0, new System.Random(1)));
        Assert.AreEqual(-1, BackgroundSpawner.MiddleTileIndex(-3, new System.Random(1)));
    }

    [Test]
    public void MatchEventTile_PicksMatchingType()
    {
        var scream = MakeEventDef(1f, ZoneInputType.Scream);
        var talk = MakeEventDef(1f, ZoneInputType.Talk);

        var result = BackgroundSpawner.MatchEventTile(new[] { scream, talk }, ZoneInputType.Talk, 0.5f);

        Assert.AreEqual(talk, result);
    }

    [Test]
    public void MatchEventTile_NoMatchReturnsNull()
    {
        var scream = MakeEventDef(1f, ZoneInputType.Scream);
        var wall = MakeDef(10f, 0, isDefault: true); // not an event tile

        var result = BackgroundSpawner.MatchEventTile(new[] { scream, wall }, ZoneInputType.Clap, 0.5f);

        Assert.IsNull(result);
    }

    [Test]
    public void MatchEventTile_WeightedAcrossMultipleMatches()
    {
        var a = MakeEventDef(1f, ZoneInputType.Talk);
        var b = MakeEventDef(3f, ZoneInputType.Talk);
        var defs = new[] { a, b };

        // total weight 4; roll01=0.1 -> target 0.4 < 1 -> a; roll01=0.9 -> target 3.6 -> b
        Assert.AreEqual(a, BackgroundSpawner.MatchEventTile(defs, ZoneInputType.Talk, 0.1f));
        Assert.AreEqual(b, BackgroundSpawner.MatchEventTile(defs, ZoneInputType.Talk, 0.9f));
    }

    [Test]
    public void PickTileType_SkipsEventTiles()
    {
        var wall = MakeDef(10f, 0, isDefault: true);
        var eventTile = MakeEventDef(100f, ZoneInputType.Scream);

        // Even with huge weight, the event tile must never be picked from the gray pool.
        var result = BackgroundSpawner.PickTileType(
            new[] { wall, eventTile },
            new Dictionary<TileDefinition, int>(),
            5f);

        Assert.AreEqual(wall, result);
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
}
