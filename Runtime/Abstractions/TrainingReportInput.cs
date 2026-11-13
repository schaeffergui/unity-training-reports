using System;
using System.Collections.Generic;

namespace TrainingReports
{
    [Serializable]
    public sealed class TrainingReportInput
    {
        public TrainingOperation Operation { get; set; }
        public string Crop { get; set; }
        public string FarmName { get; set; }
        public string SceneName { get; set; }
        public string StudentName { get; set; }
        public string InstructorName { get; set; }
        public string VehicleModel { get; set; }
        public string CompletedAt { get; set; }
        public string Duration { get; set; }
        public float ScorePercent { get; set; }
        public int ErrorCount { get; set; }
        public float AverageSpeed { get; set; }
        public float AverageRpm { get; set; }
        public float AverageThrottle { get; set; }
        public float AverageBrake { get; set; }
        public List<TrainingReportTask> Tasks { get; } = new List<TrainingReportTask>();
        public List<TrainingTelemetryPoint> Telemetry { get; } = new List<TrainingTelemetryPoint>();
        public byte[] RouteImagePng { get; set; }
    }

    [Serializable]
    public sealed class TrainingReportTask
    {
        public string Title { get; set; }
        public string Result { get; set; }
        public bool Passed { get; set; }
    }

    [Serializable]
    public struct TrainingTelemetryPoint
    {
        public float TimeSeconds;
        public float Speed;
        public float Rpm;
        public float Throttle;
        public float Brake;
    }
}
