namespace TrainingReports
{
    public static class TrainingScoreCalculator
    {
        public static float CalculatePercent(int totalTasks, int completedTasks, float penaltyPercent)
        {
            if (totalTasks <= 0)
            {
                return 0f;
            }

            float taskScore = (float)completedTasks / totalTasks * 100f;
            float score = taskScore - penaltyPercent;
            if (score < 0f) return 0f;
            if (score > 100f) return 100f;
            return score;
        }
    }
}
