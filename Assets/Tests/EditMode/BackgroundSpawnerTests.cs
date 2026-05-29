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
