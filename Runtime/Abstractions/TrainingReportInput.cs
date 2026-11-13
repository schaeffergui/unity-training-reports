namespace TrainingReports
{
    public sealed class TrainingReportInput
    {
        public TrainingOperation Operation { get; set; }
        public string Crop { get; set; }
        public string FarmName { get; set; }
        public string SceneName { get; set; }
        public string StudentName { get; set; }
        public string VehicleModel { get; set; }
    }
}
