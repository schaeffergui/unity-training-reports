using System;
using System.Collections.Generic;

namespace TrainingReports
{
    public interface IRoutePainter
    {
        bool TryGetRouteMap(out TrainingRouteMap routeMap);
    }

    [Serializable]
    public sealed class TrainingRouteMap
    {
        public string MapName { get; set; }
        public float WidthMeters { get; set; }
        public float LengthMeters { get; set; }
        public byte[] ImagePng { get; set; }
        public List<TrainingRoutePoint> Path { get; } = new List<TrainingRoutePoint>();
        public List<TrainingRouteMarker> Markers { get; } = new List<TrainingRouteMarker>();
    }

    [Serializable]
    public struct TrainingRoutePoint
    {
        public float X;
        public float Y;
        public TrainingRoutePoint(float x, float y) { X = x; Y = y; }
    }

    public enum TrainingRouteMarkerType
    {
        PlantingOn,
        PlantingOff,
        SprayOn,
        SprayOff,
        ArmsOpened,
        CutterOn,
        CutterOff,
        UnloadingPipeOpened
    }

    [Serializable]
    public struct TrainingRouteMarker
    {
        public TrainingRoutePoint Position;
        public TrainingRouteMarkerType Type;
        public TrainingRouteMarker(TrainingRoutePoint position, TrainingRouteMarkerType type)
        { Position = position; Type = type; }
    }
}
