using TrainingReports;
using UnityEngine;

[CreateAssetMenu(fileName = "LessonReportData", menuName = "Training Reports/Lesson Report Data")]
public sealed class LessonReportData : ScriptableObject, ITrainingContext
{
    public TrainingOperation operation;
    public string crop;
    public string farmName;
    public string sceneName;
    public string studentName;
    public string instructorName;
    public string vehicleModel;

    TrainingOperation ITrainingContext.Operation => operation;
    string ITrainingContext.Crop => crop;
    string ITrainingContext.FarmName => farmName;
    string ITrainingContext.SceneName => sceneName;
    string ITrainingContext.StudentName => studentName;
    string ITrainingContext.InstructorName => instructorName;
    string ITrainingContext.VehicleModel => vehicleModel;
}
