namespace TrainingReports
{
    public interface ITrainingContext
    {
        TrainingOperation Operation { get; }
        string Crop { get; }
        string FarmName { get; }
        string SceneName { get; }
        string StudentName { get; }
        string VehicleModel { get; }
    }
}
