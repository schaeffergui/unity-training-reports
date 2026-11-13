using System.Collections;
using TrainingReports;
using TrainingReports.Training;
using UnityEngine;

namespace TrainingReports.Samples
{
    public sealed class SampleTrainingAdapter : MonoBehaviour, ITrainingContext, ITrainingTheme,
        ITrainingOperationRunner, ITrainingMetricsProvider, ITrainingReportPresenter
    {
        [SerializeField] private TrainingOperation operation;
        [SerializeField] private string crop = "Sample crop";
        [SerializeField] private string farmName = "Sample farm";
        [SerializeField] private string sceneName = "Sample lesson";
        [SerializeField] private string studentName = "Student";
        [SerializeField] private string instructorName = "Instructor";
        [SerializeField] private string vehicleModel = "Sample vehicle";
        [SerializeField] private MonoBehaviour vehicleProbe;
        [SerializeField, Min(0f)] private float sampleDelaySeconds = 0.25f;

        public TrainingOperation Operation => operation;
        public string Crop => crop;
        public string FarmName => farmName;
        public string SceneName => sceneName;
        public string StudentName => studentName;
        public string InstructorName => instructorName;
        public string VehicleModel => vehicleModel;
        public float ReworkPercent => 0f;
        public float OutOfAreaPercent => 0f;
        public float ScorePenaltyPercent => 0f;
        public float WastedSeeds => 0f;
        public float WastedSprayLiters => 0f;
        public float WastedFuelLiters => 0f;
        public float EfficiencyPercent => 100f;

        public TrainingColor GetColor(TrainingThemeColor role)
        {
            return DefaultTrainingTheme.Instance.GetColor(role);
        }

        public IEnumerator RunOperation(TrainingOperation currentOperation, IVehicleProbe probe, ITrainingTaskSink tasks)
        {
            for (int index = 0; index < tasks.TaskCount && !tasks.IsCancelled; index++)
            {
                tasks.SetTaskInProgress(index);
                yield return new WaitForSeconds(sampleDelaySeconds);

                bool engineReady = probe != null && probe.EngineOn && probe.Ignition;
                string result = engineReady
                    ? string.Format("{0} sample task completed.", currentOperation)
                    : "Turn on the sample vehicle engine and ignition before starting the lesson.";
                tasks.CompleteTask(index, engineReady, result);
            }
        }

        public void StopOperation()
        {
        }

        public void ShowReport(TrainingReportInput report)
        {
            if (report == null) return;
            Debug.Log(string.Format("Sample lesson report: {0}, {1:0}% score, {2} tasks.",
                report.Operation, report.ScorePercent, report.Tasks.Count), this);
        }
    }
}
