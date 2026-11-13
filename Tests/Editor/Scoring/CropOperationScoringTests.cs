using NUnit.Framework;
using TrainingReports;

namespace TrainingReports.Tests
{
    public sealed class CropOperationScoringTests
    {
        [Test]
        public void SuccessfulTaskRatioProducesPercentage()
        {
            Assert.That(TrainingScoreCalculator.CalculatePercent(5, 4, 0f), Is.EqualTo(80f));
        }

        [Test]
        public void ReworkPenaltyIsSubtractedFromTaskScore()
        {
            Assert.That(TrainingScoreCalculator.CalculatePercent(5, 5, 12.5f), Is.EqualTo(87.5f));
        }

        [Test]
        public void EmptyTaskListProducesZero()
        {
            Assert.That(TrainingScoreCalculator.CalculatePercent(0, 0, 0f), Is.Zero);
        }
    }
}
