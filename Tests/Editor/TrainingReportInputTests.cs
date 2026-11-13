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
