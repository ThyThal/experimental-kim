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

    private static EventSpec Spec(ZoneInputType type, int count) =>
        new EventSpec { type = type, count = count };

    private static int CountLeadingFillers(System.Collections.Generic.List<LevelSlot> plan)
    {
        int n = 0;
        foreach (var slot in plan)
        {
            if (slot.kind != LevelSlotKind.Filler) break;
            n++;
        }
        return n;
    }

    [Test]
    public void BuildLevelPlan_LastSlotIsFinish()
    {
        var plan = BackgroundSpawner.BuildLevelPlan(
            new[] { Spec(ZoneInputType.Clap, 1) }, 2, 2, 1, 1, new System.Random(1));

        Assert.AreEqual(LevelSlotKind.Finish, plan[plan.Count - 1].kind);
    }

    [Test]
    public void BuildLevelPlan_TotalEventsEqualsSumOfCounts()
    {
        var specs = new[]
        {
            Spec(ZoneInputType.Clap, 1),
            Spec(ZoneInputType.Talk, 2),
            Spec(ZoneInputType.Murmur, 1),
            Spec(ZoneInputType.Scream, 3),
        };

        var plan = BackgroundSpawner.BuildLevelPlan(specs, 2, 3, 1, 2, new System.Random(7));

        int events = plan.FindAll(s => s.kind == LevelSlotKind.Event).Count;
        Assert.AreEqual(7, events);
    }

    [Test]
    public void BuildLevelPlan_StartFillerCountWithinRange()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var plan = BackgroundSpawner.BuildLevelPlan(
                new[] { Spec(ZoneInputType.Clap, 2) }, 2, 4, 1, 1, new System.Random(seed));

            int leading = CountLeadingFillers(plan);
            Assert.IsTrue(leading >= 2 && leading <= 4, $"seed {seed} gave {leading}");
        }
    }

    [Test]
    public void BuildLevelPlan_StartFillerExactWhenMinEqualsMax()
    {
        var plan = BackgroundSpawner.BuildLevelPlan(
            new[] { Spec(ZoneInputType.Clap, 1) }, 3, 3, 1, 1, new System.Random(2));

        Assert.AreEqual(3, CountLeadingFillers(plan));
    }

    [Test]
    public void BuildLevelPlan_FillerBetweenEventsWithinRange()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var plan = BackgroundSpawner.BuildLevelPlan(
                new[] { Spec(ZoneInputType.Talk, 4) }, 0, 0, 2, 5, new System.Random(seed));

            int run = 0;
            bool seenEvent = false;
            foreach (var slot in plan)
            {
                if (slot.kind == LevelSlotKind.Event)
                {
                    if (seenEvent)
                        Assert.IsTrue(run >= 2 && run <= 5, $"seed {seed} between-run {run}");
                    seenEvent = true;
                    run = 0;
                }
                else if (slot.kind == LevelSlotKind.Filler)
                {
                    run++;
                }
            }
        }
    }

    [Test]
    public void BuildLevelPlan_BetweenFillerAfterLastEventBeforeFinish()
    {
        // minBetween == maxBetween == 2: every event (including the last) is
        // followed by exactly 2 filler before the next event / the finish.
        var plan = BackgroundSpawner.BuildLevelPlan(
            new[] { Spec(ZoneInputType.Clap, 3) }, 2, 3, 2, 2, new System.Random(5));

        Assert.AreEqual(LevelSlotKind.Finish, plan[plan.Count - 1].kind);
        Assert.AreEqual(LevelSlotKind.Filler, plan[plan.Count - 2].kind);
        Assert.AreEqual(LevelSlotKind.Filler, plan[plan.Count - 3].kind);
        Assert.AreEqual(LevelSlotKind.Event, plan[plan.Count - 4].kind);
    }

    [Test]
    public void BuildLevelPlan_EventTypesMatchConfiguredCounts()
    {
        var specs = new[]
        {
            Spec(ZoneInputType.Clap, 1),
            Spec(ZoneInputType.Talk, 2),
            Spec(ZoneInputType.Scream, 3),
        };

        var plan = BackgroundSpawner.BuildLevelPlan(specs, 1, 1, 1, 1, new System.Random(9));

        int clap = 0, talk = 0, scream = 0;
        foreach (var slot in plan)
            if (slot.kind == LevelSlotKind.Event)
                switch (slot.eventType)
                {
                    case ZoneInputType.Clap: clap++; break;
                    case ZoneInputType.Talk: talk++; break;
                    case ZoneInputType.Scream: scream++; break;
                }

        Assert.AreEqual(1, clap);
        Assert.AreEqual(2, talk);
        Assert.AreEqual(3, scream);
    }

    [Test]
    public void BuildLevelPlan_NoEvents_IsStartFillerThenFinish()
    {
        var plan = BackgroundSpawner.BuildLevelPlan(
            new EventSpec[0], 2, 2, 1, 1, new System.Random(1));

        Assert.AreEqual(3, plan.Count); // 2 filler + finish
        Assert.AreEqual(LevelSlotKind.Filler, plan[0].kind);
        Assert.AreEqual(LevelSlotKind.Filler, plan[1].kind);
        Assert.AreEqual(LevelSlotKind.Finish, plan[2].kind);
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

    [Test]
    public void PickSpriteIndex_SingleSpriteAlwaysZero()
    {
        Assert.AreEqual(0, BackgroundSpawner.PickSpriteIndex(1, -1, new System.Random(1)));
        Assert.AreEqual(0, BackgroundSpawner.PickSpriteIndex(1, 0, new System.Random(2)));
    }

    [Test]
    public void PickSpriteIndex_NeverRepeatsLast()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new System.Random(seed);
            int last = 2;
            for (int i = 0; i < 20; i++)
            {
                int next = BackgroundSpawner.PickSpriteIndex(4, last, rng);
                Assert.AreNotEqual(last, next, $"seed {seed} repeated {next}");
                Assert.IsTrue(next >= 0 && next < 4, $"seed {seed} out of range {next}");
                last = next;
            }
        }
    }

    [Test]
    public void PickSpriteIndex_TwoSpritesAlternates()
    {
        Assert.AreEqual(1, BackgroundSpawner.PickSpriteIndex(2, 0, new System.Random(3)));
        Assert.AreEqual(0, BackgroundSpawner.PickSpriteIndex(2, 1, new System.Random(3)));
    }

    [Test]
    public void PickSpriteIndex_NoLastIndexStaysInRange()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            int idx = BackgroundSpawner.PickSpriteIndex(3, -1, new System.Random(seed));
            Assert.IsTrue(idx >= 0 && idx < 3, $"seed {seed} gave {idx}");
        }
    }
}
