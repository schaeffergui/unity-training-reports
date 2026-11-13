using NUnit.Framework;
using TrainingReports;
using UnityEngine;

namespace TrainingReports.Tests
{
    public sealed class TrainingReportInputTests
    {
        [Test]
        public void InputKeepsHostContextWithoutSimulatorTypes()
        {
            var input = new TrainingReportInput { Operation = TrainingOperation.HarvestingCorn, StudentName = "Alex", Crop = "Corn" };
            Assert.That(input.Operation, Is.EqualTo(TrainingOperation.HarvestingCorn));
            Assert.That(input.StudentName, Is.EqualTo("Alex"));
            Assert.That(input.Crop, Is.EqualTo("Corn"));
        }

        [Test]
        public void LessonReportDataProvidesHostContextThroughInterface()
        {
            var data = ScriptableObject.CreateInstance<LessonReportData>();
            data.operation = TrainingOperation.Planting;
            data.studentName = "Jordan";
            data.instructorName = "Taylor";
            ITrainingContext context = data;
            Assert.That(context.Operation, Is.EqualTo(TrainingOperation.Planting));
            Assert.That(context.StudentName, Is.EqualTo("Jordan"));
            Assert.That(context.InstructorName, Is.EqualTo("Taylor"));
            Object.DestroyImmediate(data);
        }

        [TestCase(TrainingOperation.Planting, 6f)]
        [TestCase(TrainingOperation.Spraying, 12f)]
        [TestCase(TrainingOperation.Harvesting, 6f)]
        [TestCase(TrainingOperation.HarvestingCorn, 6f)]
        [TestCase(TrainingOperation.Tractor, 0f)]
        [TestCase(TrainingOperation.MechanizedWalk, 0f)]
        public void RecommendedSpeedIsDefinedPerOperation(TrainingOperation operation, float expected)
        {
            Assert.That(TrainingReportInsights.GetRecommendedSpeed(operation), Is.EqualTo(expected));
        }

        [TestCase(TrainingOperation.Planting, 5.9f, false)]
        [TestCase(TrainingOperation.Planting, 6f, true)]
        [TestCase(TrainingOperation.Spraying, 11.9f, false)]
        [TestCase(TrainingOperation.Spraying, 12f, true)]
        [TestCase(TrainingOperation.Harvesting, 5.9f, false)]
        [TestCase(TrainingOperation.Harvesting, 6f, true)]
        public void SpeedObservationsRespectOperationThresholds(TrainingOperation operation, float speed, bool expectedWarning)
        {
            var report = new TrainingReportInput { Operation = operation };
            report.Telemetry.Add(new TrainingTelemetryPoint { Speed = speed });
            var observations = TrainingReportInsights.BuildObservations(report);
            bool hasWarning = observations.Exists(text => text.Contains("recommended maximum"));
            Assert.That(hasWarning, Is.EqualTo(expectedWarning));
        }

        [Test]
        public void ObservationsWarnAtSpeedLimitAndIncludeFailedTaskAdvice()
        {
            var report = new TrainingReportInput { Operation = TrainingOperation.Planting };
            report.Telemetry.Add(new TrainingTelemetryPoint { Speed = 6f });
            report.Tasks.Add(new TrainingReportTask { TaskIndex = 2, Passed = false });
            var observations = TrainingReportInsights.BuildObservations(report);
            Assert.That(observations.Exists(text => text.Contains("recommended maximum of 6 km/h")), Is.True);
            Assert.That(observations.Exists(text => text.Contains("consistent seed depth")), Is.True);
            Assert.That(observations.Exists(text => text.Contains("Review the vehicle procedures")), Is.True);
        }

        [Test]
        public void TelemetryStatisticsCalculateHarvestLossAndEfficiency()
        {
            var report = new TrainingReportInput();
            report.Telemetry.Add(new TrainingTelemetryPoint { Speed = 2f, Rpm = 800f, Throttle = 0.2f, Brake = 0.1f });
            report.Telemetry.Add(new TrainingTelemetryPoint { Speed = 4f, Rpm = 1200f, Throttle = 0.6f, Brake = 0.3f, GrainsInTruck = 90f, GrainLoss = 10f });
            var stats = TrainingReportInsights.CalculateTelemetry(report.Telemetry);
            Assert.That(stats.AverageSpeed, Is.EqualTo(3f));
            Assert.That(stats.MaximumRpm, Is.EqualTo(1200f));
            Assert.That(stats.GrainsInTruck, Is.EqualTo(90f));
            Assert.That(stats.GrainLoss, Is.EqualTo(10f));
            Assert.That(stats.HarvestEfficiencyPercent, Is.EqualTo(90f));
        }

        [Test]
        public void TasksAndTelemetryAreAvailableAsPlainData()
        {
            var input = new TrainingReportInput();
            input.Tasks.Add(new TrainingReportTask { Title = "Setup", Passed = true, Result = "Complete" });
            input.Telemetry.Add(new TrainingTelemetryPoint { TimeSeconds = 2f, Speed = 4f, Rpm = 1200f });
            Assert.That(input.Tasks[0].Passed, Is.True);
            Assert.That(input.Telemetry[0].Rpm, Is.EqualTo(1200f));
        }
    }
}
