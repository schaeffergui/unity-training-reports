using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CropOperationScoringTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component tracker;
    private Component manager;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Crop operation scoring test");
        root.SetActive(false);
        root.AddComponent<BoxCollider>().size = new Vector3(10f, 2f, 10f);
        // Os scripts da cena pertencem à Assembly-CSharp, que não pode ser referenciada por uma asmdef de testes.
        tracker = root.AddComponent(Type.GetType("PlantingZoneTracker, Assembly-CSharp", true));
        manager = root.AddComponent(Type.GetType("TrainingManager, Assembly-CSharp", true));
        SetField(tracker, "GridResolution", 10f);
        ResetTracking();
        SetField(manager, "operationTracker", tracker);
        SetField(manager, "totalTasks", 5);
        SetField(manager, "completedTasks", 5);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void DwellingInOneCellDoesNotCountAsRepeatedWork()
    {
        for (int i = 0; i < 100; i++) Record(0.25f, 0.25f);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(1));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(100f));
    }

    [Test]
    public void RepeatedPassSubtractsItsProportionFromScore()
    {
        Record(0.25f, 0.25f);
        Record(1.25f, 0.25f);
        Record(0.25f, 0.25f);
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.EqualTo(1));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(100f - 100f / 3f).Within(0.001f));
    }

    [Test]
    public void RepeatedAndOutsideWorkShareTheSameDenominator()
    {
        Sample(0.25f, 0.25f);
        Sample(1.25f, 0.25f);
        Sample(0.25f, 0.25f);
        Sample(5.25f, 0.25f);
        Assert.That(Value<float>(tracker, "RepeatedPercent"), Is.EqualTo(25f));
        Assert.That(Value<float>(tracker, "OutsidePercent"), Is.EqualTo(25f));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(50f));
    }

    [Test]
    public void InactiveTravelDoesNotAddWorkOrFillTheRoute()
    {
        Record(0.25f, 0.25f);
        Record(2.25f, 0.25f, false);
        Record(4.25f, 0.25f);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(2));
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.Zero);
    }

    [Test]
    public void SwitchingOffAndOnInPlaceDoesNotCreateAnotherPass()
    {
        Record(0.25f, 0.25f);
        Record(0.25f, 0.25f, false);
        Record(0.25f, 0.25f);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(1));
    }

    [Test]
    public void ReturningAfterInactiveTravelIsStillRepeatedWork()
    {
        Record(0.25f, 0.25f);
        Record(2.25f, 0.25f, false);
        Record(0.25f, 0.25f);
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.EqualTo(1));
    }

    [Test]
    public void FastAndSlowPassesVisitTheSameCells()
    {
        Record(0.25f, 0.25f);
        Record(4.25f, 0.25f);
        int fastCount = Value<int>(tracker, "WorkedCells");
        ResetTracking();
        for (int i = 0; i <= 40; i++) Record(0.25f + i * 0.1f, 0.25f);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(fastCount));
        Assert.That(fastCount, Is.EqualTo(5));
    }

    [Test]
    public void ZoneRotationScaleAndTerrainHeightAreRespected()
    {
        root.transform.SetPositionAndRotation(new Vector3(20f, 5f, 10f), Quaternion.Euler(0f, 40f, 0f));
        root.transform.localScale = new Vector3(2f, 1f, 3f);
        Record(4.25f, 0.25f, true, 30f);
        Record(5.25f, 0.25f, true, 30f);
        Assert.That(Value<int>(tracker, "OutsideCells"), Is.EqualTo(1));
        Assert.That(Value<float>(tracker, "OutsidePercent"), Is.EqualTo(50f));
    }

    [Test]
    public void PenaltiesCannotMakeScoreNegative()
    {
        SetField(manager, "completedTasks", 1);
        Record(5.25f, 0.25f);
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.Zero);
    }

    [Test]
    public void PartialBoundaryCellStillDetectsLeavingTheZone()
    {
        root.GetComponent<BoxCollider>().size = new Vector3(9.5f, 2f, 10f);
        ResetTracking();
        Record(4.5f, 0.25f);
        Record(4.9f, 0.25f);
        Assert.That(Value<int>(tracker, "OutsideCells"), Is.EqualTo(1));
    }

    [Test]
    public void EmptySessionDoesNotDeductPoints()
    {
        Assert.That(Value<float>(tracker, "PenaltyPercent"), Is.Zero);
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(100f));
    }

    [TestCase(false, 0)]
    [TestCase(true, 1)]
    public void HarvestingTracksEmptyGroundOnlyWithSeparatorEnabled(bool separatorEnabled, int expectedCells)
    {
        var harvester = root.AddComponent<RDHarvesterController>();
        harvester.CurrentRotationSpeed = 100f;
        harvester.IsHarvesting = false;
        SetField(harvester, "<SeparatorEnabled>k__BackingField", separatorEnabled);
        SetField(manager, "isRunning", true);
        SetField(tracker, "trainingManager", manager);
        tracker.GetType().GetMethod("StartTrackingHarvester").Invoke(tracker, new object[] { harvester });
        tracker.GetType().GetMethod("LateUpdate", PrivateInstance).Invoke(tracker, null);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(expectedCells));
    }

    [Test]
    public void ResetClearsCoverageAndPenalties()
    {
        Record(0.25f, 0.25f);
        Record(5.25f, 0.25f);
        ResetTracking();
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.Zero);
        Assert.That(Value<float>(tracker, "PenaltyPercent"), Is.Zero);
        Assert.That((float)tracker.GetType().GetMethod("GetProgressPercent").Invoke(tracker, null), Is.Zero);
        Record(0.25f, 0.25f);
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.Zero);
    }

    [Test]
    public void SprayerPenalizesThePartOfTheBoomOutsideTheZone()
    {
        var sprayer = CreateSprayer();
        SprayAt(sprayer, 4.25f, 0.25f, 1f);

        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(5));
        Assert.That(Value<float>(tracker, "OutsidePercent"), Is.EqualTo(40f));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(60f));
    }

    [Test]
    public void AdjacentSprayPassCountsOverlapEvenWithADifferentCenterline()
    {
        var sprayer = CreateSprayer();
        SprayAt(sprayer, 0.25f, 0.25f, 1f, 1f);
        SprayAt(sprayer, 2.25f, 0.25f, 0f, 1f);
        SprayAt(sprayer, 3.25f, 0.25f, 1f, 2f);

        Assert.That(Value<float>(tracker, "RepeatedPercent"), Is.EqualTo(20f));
        Assert.That(Value<float>(tracker, "OutsidePercent"), Is.EqualTo(10f));
        Assert.That(Value<float>(manager, "WastedSprayLiters"), Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(Value<float>(manager, "WastedFuelLiters"), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(70f));
    }

    [Test]
    public void ContinuousSprayPassDoesNotCountAsOverlapWhileMoving()
    {
        var sprayer = CreateSprayer();
        SprayAt(sprayer, 0.25f, 0.25f, 1f);
        SprayAt(sprayer, 0.25f, 0.75f, 1f);
        SprayAt(sprayer, 0.25f, 1.25f, 1f);

        Assert.That(Value<float>(tracker, "RepeatedPercent"), Is.Zero);
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(10));
    }

    [Test]
    public void StationarySprayCountsTheAdditionalLiquidWithoutAddingCoverage()
    {
        var sprayer = CreateSprayer();
        SprayAt(sprayer, 0.25f, 0.25f, 1f);
        SprayAt(sprayer, 0.25f, 0.25f, 3f);

        Assert.That(Value<float>(tracker, "RepeatedPercent"), Is.EqualTo(75f));
        Assert.That(Value<int>(tracker, "WorkedCells"), Is.EqualTo(5));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(25f));
    }

    [Test]
    public void ImplementFootprintIncludesItsDepthAndPenalizesReturnPass()
    {
        var record = tracker.GetType().GetMethod("RecordImplementFootprint", PrivateInstance);
        var edge = new RDImplementFootprint(new Vector3(-1.75f, 0, 4.25f), new Vector3(2.25f, 0, 4.25f), 1f);
        record.Invoke(tracker, new object[] { edge, 0f, 0f });

        Assert.That(Value<int>(tracker, "OutsideCells"), Is.GreaterThan(0));
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.Zero);
        record.Invoke(tracker, new object[] { edge, 0f, 0f });
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.Zero);
        tracker.GetType().GetMethod("ClearFootprintContinuity", PrivateInstance).Invoke(tracker, null);
        record.Invoke(tracker, new object[] { edge, 0f, 0f });
        Assert.That(Value<int>(tracker, "RepeatedCells"), Is.GreaterThan(0));
    }

    [Test]
    public void PlantingAttributesOnlyRepeatedCoverageToSeedAndFuelWaste()
    {
        var record = tracker.GetType().GetMethod("RecordImplementFootprint", PrivateInstance);
        var first = new RDImplementFootprint(new Vector3(-1.75f, 0f, 0.25f), new Vector3(2.25f, 0f, 0.25f));
        var second = new RDImplementFootprint(new Vector3(1.25f, 0f, 0.25f), new Vector3(5.25f, 0f, 0.25f));
        record.Invoke(tracker, new object[] { first, 6f, 1f });
        Assert.That(Value<float>(manager, "WastedSeeds"), Is.Zero);
        Assert.That(Value<float>(manager, "WastedFuelLiters"), Is.Zero);

        tracker.GetType().GetMethod("ClearFootprintContinuity", PrivateInstance).Invoke(tracker, null);
        record.Invoke(tracker, new object[] { second, 6f, 2f });
        // Duas das cinco células foram replantadas, e uma ficou fora do talhão.
        Assert.That(Value<float>(manager, "WastedSeeds"), Is.EqualTo(2.4f).Within(0.001f));
        Assert.That(Value<float>(manager, "WastedFuelLiters"), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(Value<float>(manager, "CorrectPercentage"), Is.EqualTo(70f));

        ResetTracking();
        Assert.That(Value<float>(manager, "WastedSeeds"), Is.Zero);
        Assert.That(Value<float>(manager, "WastedFuelLiters"), Is.Zero);
    }

    private RDSprayerController CreateSprayer()
    {
        var machine = new GameObject("Sprayer");
        machine.transform.SetParent(root.transform, false);
        var sprayer = machine.AddComponent<RDSprayerController>();
        sprayer.ArmsOpened = true;
        var nozzles = new ParticleSystem[3];
        for (int i = 0; i < nozzles.Length; i++)
        {
            var nozzle = new GameObject("Nozzle");
            nozzle.transform.SetParent(machine.transform, false);
            nozzle.transform.localPosition = new Vector3((i - 1) * 2f, 0f, 0f);
            nozzles[i] = nozzle.AddComponent<ParticleSystem>();
        }
        SetField(sprayer, "SprayAnimation", nozzles);
        tracker.GetType().GetMethod("StartTrackingSpraying").Invoke(tracker, new object[] { sprayer });
        return sprayer;
    }

    private void SprayAt(RDSprayerController sprayer, float x, float z, float liters, float fuel = 0f)
    {
        sprayer.transform.localPosition = new Vector3(x, 0f, z);
        tracker.GetType().GetMethod("TrackSpraying", PrivateInstance).Invoke(tracker, new object[] { liters, 1f, fuel });
    }

    private void Record(float x, float z, bool active = true, float height = 0f)
    {
        tracker.GetType().GetMethod("RecordOperation", PrivateInstance).Invoke(tracker, new object[] { root.transform.TransformPoint(new Vector3(x, height, z)), active });
    }

    private void Sample(float x, float z)
    {
        tracker.GetType().GetMethod("RecordCell", PrivateInstance).Invoke(tracker, new object[] { new Vector3(x, 0f, z) });
    }

    private void ResetTracking()
    {
        tracker.GetType().GetMethod("ResetTracking").Invoke(tracker, null);
    }

    private static T Value<T>(Component target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }

    private static void SetField(Component target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
