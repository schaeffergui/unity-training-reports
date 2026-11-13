using NUnit.Framework;
using TrainingReports;

namespace TrainingReports.Tests
{
    public sealed class ImplementFootprintTests
    {
        [TestCase(100, 100, 0f, 100f)]
        [TestCase(100, 50, 0f, 50f)]
        [TestCase(100, 50, 10f, 40f)]
        [TestCase(10, 0, 15f, 0f)]
        public void ScoreClampsTaskAndPenaltyValues(int total, int completed, float penalty, float expected)
        {
            Assert.That(TrainingScoreCalculator.CalculatePercent(total, completed, penalty), Is.EqualTo(expected));
        }

        [Test]
        public void PenaltyCannotRaiseScoreAboveOneHundred()
        {
            Assert.That(TrainingScoreCalculator.CalculatePercent(4, 8, -25f), Is.EqualTo(100f));
        }
    }
}
