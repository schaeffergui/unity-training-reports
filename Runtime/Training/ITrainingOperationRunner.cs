using System.Collections;
using TrainingReports;

namespace TrainingReports.Training
{
    public interface ITrainingTaskSink
    {
        bool IsCancelled { get; }
        int TaskCount { get; }
        void SetTaskInProgress(int index);
        void CompleteTask(int index, bool succeeded, string message);
    }

    public interface ITrainingOperationRunner
    {
        IEnumerator RunOperation(TrainingOperation operation, IVehicleProbe vehicle, ITrainingTaskSink tasks);
        void StopOperation();
    }

    public interface ITrainingMetricsProvider
    {
        float ReworkPercent { get; }
        float OutOfAreaPercent { get; }
        float ScorePenaltyPercent { get; }
        float WastedSeeds { get; }
        float WastedSprayLiters { get; }
        float WastedFuelLiters { get; }
        float EfficiencyPercent { get; }
    }

    public interface ITrainingReportPresenter
    {
        void ShowReport(TrainingReportInput report);
    }
}
