using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ImplementFootprintTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Ready = "Assets/Simulator/Vehicles/Car2/Prefabs/Ready/";

    private static object Call(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, Members).Invoke(target, arguments);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Members).SetValue(target, value);
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Members).GetValue(target);
    }

    [TestCase("Machine_8_Tractor_A.prefab")]
    [TestCase("Machine_275_Tractor_A_Planter.prefab")]
    [TestCase("Machine_9_Sprayer.prefab")]
    [TestCase("Machine_10_Harvester_L500.prefab")]
    [TestCase("Machine_10_Harvester_L500_Corn.prefab")]
    [TestCase("Machine_283_Harvester_L500_Corn Variant.prefab")]
    [TestCase("Machine_285_Harvester_L500_wheat Variant.prefab")]
    [TestCase("Machine_286_Harvester_L500_Wheat_35ft.prefab")]
    public void GpsKeepsTheVisiblePlayerCenteredAndUprightForEveryVehicle(string prefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Ready + prefab);
        GameObject dashboard = PrefabUtility.LoadPrefabContents("Assets/Scene elements/Prefabs/RDADashboard.prefab");
        try
        {
            dashboard.transform.localScale = Vector3.one;
            Component map = dashboard.GetComponentInChildren(Type.GetType("MinimapController, Assembly-CSharp", true), true);
            RectTransform content = Get<RectTransform>(map, "contentRectTransform");
            RectTransform viewport = (RectTransform)content.parent;
            var marker = new GameObject("MinimapIconGO");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, root.scene);
            Component world = marker.AddComponent(Type.GetType("MinimapWorldObject, Assembly-CSharp", true));
            Call(map, "RegisterMinimapWorldObject", world, false);
            marker.transform.SetParent(root.transform, false);
            Call(map, "RegisterMinimapWorldObject", world, true);
            RectTransform playerIcon = Get<RectTransform>(Get<object>(map, "followIcon0"), "IconRectTransform");
            Vector2[] farms = { new Vector2(233f, 501f), new Vector2(647f, 663.5f) };
            Vector2[] positions = { new Vector2(0.02f, 0.03f), new Vector2(0.5f, 0.5f), new Vector2(0.98f, 0.97f) };
            float[] zooms = { 1f, 1.15f, 1.3f, 3f };
            foreach (Vector2 farm in farms)
            {
                Call(map, "SetMapDimensions", farm, new Vector2(-15f, 30f));
                foreach (Vector2 position in positions)
                foreach (float zoom in zooms)
                for (int yaw = 0; yaw < 360; yaw += 15)
                foreach (bool rotating in new[] { false, true })
                {
                    root.transform.SetPositionAndRotation(new Vector3(farm.x * position.x - 15f, 20f, farm.y * position.y + 30f), Quaternion.Euler(8f, yaw, -5f));
                    content.localScale = Vector3.one * zoom;
                    Call(map, "SetMapRotationMode", rotating);
                    content.localRotation = Quaternion.Euler(0f, 0f, rotating ? yaw - 45f : 0f);
                    Set(map, "currentMapRotation", content.localRotation);
                    Call(map, "UpdateMiniMapIcons");
                    Vector2 visibleCenter = viewport.InverseTransformPoint(playerIcon.TransformPoint(playerIcon.rect.center));
                    Assert.That(Vector2.Distance(visibleCenter, viewport.rect.center), Is.LessThan(0.001f), "Player center at heading " + yaw);
                    if (rotating)
                    {
                        Vector3 direction = viewport.InverseTransformDirection(playerIcon.up);
                        Assert.That(Vector3.Angle(direction, Vector3.up), Is.LessThan(0.01f), "GPS arrow at heading " + yaw);
                    }
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(dashboard);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase("Machine_283_Harvester_L500_Corn Variant.prefab", 5.416f, 4.022f)]
    [TestCase("Machine_285_Harvester_L500_wheat Variant.prefab", 5.68f, 3.95517f)]
    [TestCase("Machine_286_Harvester_L500_Wheat_35ft.prefab", 10.668f, 3.95517f)]
    public void ActualHeaderWidthAndOffsetStayConstantThroughFullRotation(string prefab, float width, float offset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Ready + prefab);
        try
        {
            root.transform.position = new Vector3(120f, 20f, 250f);
            RDHarvesterController harvester = root.GetComponent<RDHarvesterController>();
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Assert.That(harvester.TryGetWorkArea(out RDImplementFootprint area), Is.True);
                Assert.That(area.Width, Is.EqualTo(width).Within(0.001f), "Heading " + yaw);
                Assert.That(Vector3.Dot(area.Center - root.transform.position, root.transform.forward), Is.EqualTo(offset).Within(0.001f));
                Assert.That(area.Contains(root.transform.position), Is.False);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase(233f, 501f)]
    [TestCase(647f, 663.5f)]
    public void SprayerUiMatchesLiveNozzlesDuringFoldingAndEveryHeading(float width, float length)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Ready + "Machine_9_Sprayer.prefab");
        GameObject dashboard = PrefabUtility.LoadPrefabContents("Assets/Scene elements/Prefabs/RDADashboard.prefab");
        try
        {
            Component map = dashboard.GetComponentInChildren(Type.GetType("MinimapController, Assembly-CSharp", true), true);
            RectTransform content = Get<RectTransform>(map, "contentRectTransform");
            Call(map, "SetMapDimensions", new Vector2(width, length), new Vector2(-15f, 30f));
            var marker = new GameObject("MinimapIconGO");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, root.scene);
            marker.transform.SetParent(root.transform, false);
            Component world = marker.AddComponent(Type.GetType("MinimapWorldObject, Assembly-CSharp", true));
            Call(map, "RegisterMinimapWorldObject", world, true);
            RectTransform[] icons = Get<RectTransform[]>(map, "implementIcons");
            var lookup = Get<System.Collections.IDictionary>(map, "miniMapWorldObjectsLookup");
            RectTransform bodyIcon = Get<RectTransform>(lookup[world], "IconRectTransform");
            var iconPrefab = Get<Component>(map, "minimapIconPrefab");
            Vector2 markerSize = Get<RectTransform>(iconPrefab, "IconRectTransform").rect.size;
            RDSprayerController sprayer = root.GetComponent<RDSprayerController>();
            root.transform.position = new Vector3(width * 0.5f - 15f, 20f, length * 0.5f + 30f);
            float mapScale = 600f / Mathf.Max(width, length);
            Assert.That(content.rect.width / width, Is.EqualTo(content.rect.height / length).Within(0.00001f));

            for (int pose = 0; pose < 3; pose++)
            {
                Call(sprayer, "SetArmsStatus", pose == 2);
                if (pose == 1)
                {
                    sprayer.AreTheArmsInPositionToClose = true;
                    sprayer.Speed = 90f;
                    Call(sprayer, "UpdateArms", true, true, 0.02f);
                    Call(sprayer, "UpdateArms", true, true, 0.02f);
                }
                if (pose == 2)
                {
                    for (int i = 0; i < sprayer.WorkAreaCount; i++)
                    {
                        Assert.That(sprayer.TryGetWorkArea(i, out _), Is.True,
                            "The open boom must keep continuous coverage at the joins between sections.");
                    }
                }
                for (int yaw = 0; yaw < 360; yaw += 15)
                foreach (bool rotating in new[] { false, true })
                foreach (float zoom in new[] { 1f, 1.15f, 1.3f, 3f })
                {
                    root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    content.localScale = Vector3.one * zoom;
                    Call(map, "SetMapRotationMode", rotating);
                    content.localRotation = Quaternion.Euler(0f, 0f, rotating ? yaw - 15f : 0f);
                    Set(map, "currentMapRotation", content.localRotation);
                    Call(map, "UpdateMiniMapIcons");
                    // The chassis marker keeps its visual size when zooming.
                    // Os segmentos da barra abaixo continuam em escala de terreno.
                    Vector2 visibleMarkerSize = bodyIcon.rect.size * (bodyIcon.localScale.x * zoom);
                    Assert.That(Vector2.Distance(visibleMarkerSize, markerSize), Is.LessThan(0.001f));
                    for (int i = 0; i < icons.Length; i++)
                    {
                        bool valid = sprayer.TryGetWorkArea(i, out RDImplementFootprint area);
                        Assert.That(icons[i].gameObject.activeSelf, Is.EqualTo(valid));
                        if (!valid) continue;
                        Vector2 expectedStart = new Vector2((area.Start.x + 15f - width * 0.5f) * mapScale, (area.Start.z - 30f - length * 0.5f) * mapScale);
                        Vector2 expectedEnd = new Vector2((area.End.x + 15f - width * 0.5f) * mapScale, (area.End.z - 30f - length * 0.5f) * mapScale);
                        Vector3 half = icons[i].localRotation * Vector3.right * icons[i].rect.width * 0.5f;
                        Assert.That(Vector2.Distance((Vector2)(icons[i].localPosition - half), expectedStart), Is.LessThan(0.001f));
                        Assert.That(Vector2.Distance((Vector2)(icons[i].localPosition + half), expectedEnd), Is.LessThan(0.001f));
                    }
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(dashboard);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase(null, 2.7f, 4f)]
    [TestCase("Machine_286_Harvester_L500_Wheat_35ft.prefab", 5.334f, 3.955178f)]
    public void CropEditsFollowTheRotatedHeaderAndDoNotTouchTheVehicleOrigin(string prefab, float halfWidth, float offset)
    {
        var root = prefab == null ? new GameObject("Header terrain test") : PrefabUtility.LoadPrefabContents(Ready + prefab);
        root.SetActive(false);
        RDHarvesterController harvester = prefab == null ? root.AddComponent<RDHarvesterController>() : root.GetComponent<RDHarvesterController>();
        var texture = new Texture2D(2, 2);
        var data = new TerrainData { size = new Vector3(64f, 1f, 64f) };
        data.SetDetailResolution(64, 8);
        var prototype = new DetailPrototype { prototypeTexture = texture, renderMode = DetailRenderMode.GrassBillboard, minWidth = 1f, maxWidth = 1f, minHeight = 1f, maxHeight = 1f };
        data.detailPrototypes = new[] { prototype, prototype };
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        try
        {
            Set(harvester, "mainTerrain", terrainObject.GetComponent<Terrain>());
            Set(harvester, "isInitialized", true);
            Set(harvester, "maxDetailLayers", 2);
            var original = new int[64, 64];
            for (int z = 0; z < 64; z++)
            for (int x = 0; x < 64; x++) original[z, x] = 1;
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                data.SetDetailLayer(0, 0, 0, original);
                data.SetDetailLayer(0, 0, 1, new int[64, 64]);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                root.transform.SetPositionAndRotation(new Vector3(32f, 0f, 32f), rotation);
                Vector3 center = root.transform.position + rotation * Vector3.forward * offset;
                var area = new RDImplementFootprint(center - rotation * Vector3.right * halfWidth, center + rotation * Vector3.right * halfWidth, 0.6f);
                if (prefab != null) Assert.That(harvester.TryGetWorkArea(out area), Is.True);
                Assert.That(Call(harvester, "ReplaceDetails", area, 0, 1), Is.True);
                int[,] result = data.GetDetailLayer(0, 0, 64, 64, 1);
                for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                {
                    Vector3 point = Quaternion.Inverse(rotation) * (new Vector3(x + 0.5f, 0f, z + 0.5f) - center);
                    bool expected = Mathf.Abs(point.x) <= halfWidth && Mathf.Abs(point.z) <= 0.6f;
                    Assert.That(result[z, x] != 0, Is.EqualTo(expected), "Heading " + yaw + ", cell " + x + ", " + z);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(terrainObject);
            if (prefab == null) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(233f, 501f, 398, 859)]
    [TestCase(647f, 663.5f, 826, 861)]
    public void PaintingUsesWorldGeometryRegardlessOfCanvasAndZoom(float width, float length, int pixelsX, int pixelsY)
    {
        var root = new GameObject("World map painting test");
        root.SetActive(false);
        Component map = root.AddComponent(Type.GetType("MinimapController, Assembly-CSharp", true));
        Component painter = root.AddComponent(Type.GetType("RDMapPainter, Assembly-CSharp", true));
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(root.transform, false);
        content.sizeDelta = new Vector2(600f, 600f);
        Set(map, "contentRectTransform", content);
        Call(map, "SetMapDimensions", new Vector2(width, length), Vector2.zero);
        var texture = new Texture2D(pixelsX, pixelsY, TextureFormat.RGBA32, false);
        Set(painter, "copyTexture", texture);
        Set(painter, "minimapController", map);
        Set(painter, "pixelColor", Color.clear);
        Canvas canvas = root.AddComponent<Canvas>();
        var blank = new Color32[pixelsX * pixelsY];
        for (int i = 0; i < blank.Length; i++) blank[i] = new Color32(255, 255, 255, 255);
        try
        {
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                Color32[] first = null;
                foreach (RenderMode mode in new[] { RenderMode.ScreenSpaceOverlay, RenderMode.ScreenSpaceCamera })
                {
                    texture.SetPixels32(blank);
                    canvas.renderMode = mode;
                    content.localRotation = Quaternion.Euler(0f, 0f, yaw);
                    content.localScale = Vector3.one * (mode == RenderMode.ScreenSpaceOverlay ? 1.15f : 3f);
                    Vector3 vehicle = new Vector3(width * 0.5f, 0f, length * 0.5f);
                    Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 center = vehicle + rotation * Vector3.forward * 4f;
                    var area = new RDImplementFootprint(center - rotation * Vector3.right * 2.7f, center + rotation * Vector3.right * 2.7f, 0.6f);
                    Assert.That(Call(painter, "PaintWorkArea", area), Is.True);
                    Color32[] result = texture.GetPixels32();
                    Assert.That(result[(pixelsY / 2) * pixelsX + pixelsX / 2].a, Is.EqualTo(255));
                    if (first != null)
                    {
                        bool identical = true;
                        for (int i = 0; i < result.Length; i++)
                        {
                            if (first[i].a == result[i].a) continue;
                            identical = false;
                            break;
                        }
                        Assert.That(identical, Is.True, "Canvas mode changed coverage at heading " + yaw);
                    }
                    first = result;
                }
            }
        }
        finally
        {
            Set(painter, "copyTexture", null);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void LessonCoverageUsesTheCutterFootprintInsteadOfLegacyWidthOrVehicleOrigin()
    {
        var root = new GameObject("Lesson footprint test");
        root.SetActive(false);
        Component trigger = root.AddComponent(Type.GetType("DefinedAreaTrigger, Assembly-CSharp", true));
        var task = ScriptableObject.CreateInstance(Type.GetType("RDLessonTask, Assembly-CSharp", true));
        Type taskType = task.GetType().GetField("Type").FieldType;
        Set(task, "Type", Enum.Parse(taskType, "WorkArea"));
        Set(trigger, "Task", task);
        try
        {
            int[] counts = new int[2];
            for (int i = 0; i < counts.Length; i++)
            {
                Set(task, "WorkWidth", i == 0 ? 4f : 100f);
                Call(trigger, "SetDimensions", new Vector3(40f, 2f, 40f));
                Call(trigger, "RecordWorkArea", new RDImplementFootprint(new Vector3(-2.7f, 0f, 4f), new Vector3(2.7f, 0f, 4f), 0.6f));
                bool[] cells = Get<bool[]>(trigger, "coveredCells");
                Assert.That(cells[20 * 40 + 20], Is.False);
                counts[i] = Get<int>(trigger, "coveredCount");
                Assert.That(counts[i], Is.GreaterThan(0));
            }
            Assert.That(counts[0], Is.EqualTo(counts[1]));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(task);
        }
    }

    [Test]
    public void SweepsFillMovementButDoNotBridgeTeleports()
    {
        var first = new RDImplementFootprint(Vector3.left * 3f, Vector3.right * 3f, 0.5f);
        var moved = new RDImplementFootprint(first.Start + Vector3.forward * 4f, first.End + Vector3.forward * 4f, 0.5f);
        Assert.That(moved.GetSweepSteps(first, 0.5f), Is.EqualTo(8));
        Assert.That(RDImplementFootprint.Lerp(first, moved, 0.5f).Contains(Vector3.forward * 2f), Is.True);
        var teleported = new RDImplementFootprint(first.Start + Vector3.forward * 100f, first.End + Vector3.forward * 100f, 0.5f);
        Assert.That(teleported.GetSweepSteps(first, 0.5f), Is.Zero);
        Assert.That(default(RDImplementFootprint).Contains(Vector3.zero), Is.False);
    }
}
