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
        public float ReworkPercent { get; set; }
        public float OutOfAreaPercent { get; set; }
        public float ScorePenaltyPercent { get; set; }
        public float WastedSeeds { get; set; }
        public float WastedSprayLiters { get; set; }
        public float WastedFuelLiters { get; set; }
        public float EfficiencyPercent { get; set; }
        public List<TrainingReportTask> Tasks { get; } = new List<TrainingReportTask>();
        public List<TrainingTelemetryPoint> Telemetry { get; } = new List<TrainingTelemetryPoint>();
    }

    [Serializable]
    public sealed class TrainingReportTask
    {
        public int TaskIndex { get; set; }
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
        public float GrainsInTruck;
        public float GrainLoss;
    }

    public struct TrainingReportTelemetryStats
    {
        public int SampleCount;
        public float AverageSpeed;
        public float MaximumSpeed;
        public float AverageRpm;
        public float MaximumRpm;
        public float AverageThrottle;
        public float AverageBrake;
        public float GrainsInTruck;
        public float GrainLoss;
        public float HarvestEfficiencyPercent;
    }

    public static class TrainingReportInsights
    {
        public static float GetRecommendedSpeed(TrainingOperation operation)
        {
            switch (operation)
            {
                case TrainingOperation.Planting: return 6f;
                case TrainingOperation.Spraying: return 12f;
                case TrainingOperation.Harvesting:
                case TrainingOperation.HarvestingCorn: return 6f;
                default: return 0f;
            }
        }

        public static TrainingReportTelemetryStats CalculateTelemetry(IList<TrainingTelemetryPoint> points)
        {
            var stats = new TrainingReportTelemetryStats();
            if (points == null || points.Count == 0) return stats;
            stats.SampleCount = points.Count;
            float speed = 0f, rpm = 0f, throttle = 0f, brake = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                TrainingTelemetryPoint point = points[i];
                speed += point.Speed;
                rpm += point.Rpm;
                throttle += point.Throttle;
                brake += point.Brake;
                if (point.Speed > stats.MaximumSpeed) stats.MaximumSpeed = point.Speed;
                if (point.Rpm > stats.MaximumRpm) stats.MaximumRpm = point.Rpm;
            }
            TrainingTelemetryPoint last = points[points.Count - 1];
            stats.AverageSpeed = speed / points.Count;
            stats.AverageRpm = rpm / points.Count;
            stats.AverageThrottle = throttle / points.Count;
            stats.AverageBrake = brake / points.Count;
            stats.GrainsInTruck = last.GrainsInTruck;
            stats.GrainLoss = last.GrainLoss;
            float totalGrain = stats.GrainsInTruck + stats.GrainLoss;
            stats.HarvestEfficiencyPercent = totalGrain > 0f ? stats.GrainsInTruck / totalGrain * 100f : 0f;
            return stats;
        }

        public static List<string> BuildObservations(TrainingReportInput report)
        {
            var observations = new List<string>();
            if (report == null) return observations;
            TrainingReportTelemetryStats stats = CalculateTelemetry(report.Telemetry);
            float limit = GetRecommendedSpeed(report.Operation);
            if (limit > 0f && stats.SampleCount > 0 && stats.AverageSpeed >= limit)
                observations.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    GetSpeedObservation(report.Operation), stats.AverageSpeed, limit));
            for (int i = 0; i < report.Tasks.Count; i++)
            {
                TrainingReportTask task = report.Tasks[i];
                if (task == null || task.Passed) continue;
                string advice = GetTaskAdvice(report.Operation, task.TaskIndex);
                if (!string.IsNullOrEmpty(advice)) observations.Add(advice);
            }
            if (report.Tasks.Exists(task => task != null && !task.Passed))
                observations.Add("Review the vehicle procedures associated with the unsuccessful tasks.");
            if (observations.Count == 0) observations.Add("No additional observations.");
            return observations;
        }

        public static string GetTaskAdvice(TrainingOperation operation, int taskIndex)
        {
            switch (operation)
            {
                case TrainingOperation.Harvesting:
                case TrainingOperation.HarvestingCorn:
                    switch (taskIndex)
                    {
                        case 0: return "Check all safety systems before starting the engine.";
                        case 1: return "Set the platform height correctly to reduce grain loss and equipment damage.";
                        case 2: return "Maintain a steady alignment to process the full work area without gaps.";
                        case 3: return "Switch off the reel and threshing systems at the end of a harvest pass.";
                        case 4: return "Position the unloading pipe correctly over the truck to prevent spillage.";
                        case 5: return "Close the unloading pipe before moving or parking the machine.";
                    }
                    break;
                case TrainingOperation.Planting:
                    switch (taskIndex)
                    {
                        case 0: return "Check seed and fertilizer levels before starting.";
                        case 1: return "Raise the implement before maneuvering or accelerating.";
                        case 2: return "Maintain a steady planting speed for consistent seed depth and spacing.";
                        case 3: return "Raise the implement smoothly at the field edge to protect the rows.";
                        case 4: return "Check seed tubes for blockages after the operation.";
                        case 5: return "Park on level ground and release hydraulic pressure before storage.";
                    }
                    break;
                case TrainingOperation.Tractor:
                case TrainingOperation.MechanizedWalk:
                    switch (taskIndex)
                    {
                        case 0: return "Follow the correct engine start procedure to protect engine components.";
                        case 1: return "Follow the route markers to maintain awareness of the implement.";
                        case 2: return "Select a gear suited to the load to avoid overload and excess fuel use.";
                        case 3: return "Use mirrors and reduce speed when maneuvering in tight spaces.";
                        case 4: return "Check implement hitch and hydraulic connections before travel.";
                        case 5: return "Engage the handbrake and lower the implement before parking.";
                    }
                    break;
                case TrainingOperation.Spraying:
                    switch (taskIndex)
                    {
                        case 0: return "Check system pressure and nozzle condition before spraying.";
                        case 1: return "Open spray booms only in wide, obstacle-free areas.";
                        case 2: return "Monitor wind and working pressure to reduce product drift.";
                        case 3: return "Switch off the spray pump while turning at the headland.";
                        case 4: return "Keep the boom at a stable height above the crop.";
                        case 5: return "Clean the system after use to prevent residue from blocking nozzles.";
                    }
                    break;
            }
            return string.Empty;
        }

        private static string GetSpeedObservation(TrainingOperation operation)
        {
            switch (operation)
            {
                case TrainingOperation.Spraying: return "Average speed was {0:0.0} km/h, at or above the recommended maximum of {1:0.#} km/h. This can reduce application uniformity and increase product drift.";
                case TrainingOperation.Planting: return "Average speed was {0:0.0} km/h, at or above the recommended maximum of {1:0.#} km/h. This can cause uneven depth, gaps, and yield loss.";
                default: return "Average speed was {0:0.0} km/h, at or above the recommended maximum of {1:0.#} km/h. This can increase crop loss during harvesting.";
            }
        }
    }
}
