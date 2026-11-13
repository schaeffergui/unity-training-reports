using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TrainingReports;
using TrainingReports.Training;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TrainingManager : MonoBehaviour, ITrainingTaskSink
    {
        public static TrainingManager Instance { get; private set; }

        [Header("Host integration")]
        [SerializeField] private MonoBehaviour lessonAdapter;

        [Header("Training panels")]
        public GameObject TractorTrainingUIObject;
        public GameObject PlantingTrainingUIObject;
        public GameObject SprayingTrainingUIObject;
        public GameObject HarvestingTrainingUIObject;
        public GameObject Report;
        public GameObject Garage;
        public GameObject Truck;
        public GameObject OpenWoodenGate;
        public GameObject ClosedWoodenGate;
        public GameObject TractorPath;
        public GameObject TractorBackCollider;
        public GameObject[] TractorCylinderGameObjects;

        [Header("Status")]
        public TextMeshProUGUI DateText;
        public TextMeshProUGUI TimerText;
        public TextMeshProUGUI TrainingText;
        public TextMeshProUGUI NumberErrorsText;
        public TextMeshProUGUI PercentageCorrectText;
        public bool IsVehicleOutside;
        public bool TaskCompleted;
        public bool Task3HarvestingCompleted;
        public bool Task3HarvestingSuccess;

        [Header("UI Focus")]
        [Tooltip("The button that should be focused when the Report panel opens.")]
        public GameObject ReportFirstButtonToFocus;

        private TrainingTaskUI currentTaskUI;
        private float elapsedTime;
        private bool isRunning;
        private int totalTasks;
        private int completedTasks;
        private int errorCount;
        private Coroutine operationCoroutine;
        private readonly Dictionary<int, string> taskMessages = new Dictionary<int, string>();

        [Serializable]
        public sealed class TrainingTaskResult
        {
            public int TaskIndex;
            public string TaskName;
            public bool Success;
            public string Message;
        }

        public List<TrainingTaskResult> TaskResults = new List<TrainingTaskResult>();

        private ITrainingContext Context => lessonAdapter as ITrainingContext;
        private IVehicleProbe Vehicle => lessonAdapter as IVehicleProbe;
        private ITrainingOperationRunner OperationRunner => lessonAdapter as ITrainingOperationRunner;
        private ITrainingMetricsProvider Metrics => lessonAdapter as ITrainingMetricsProvider;
        private ITrainingReportPresenter ReportPresenter => lessonAdapter as ITrainingReportPresenter;

        public bool IsTrainingRunning => isRunning;
        public bool IsCancelled => !isRunning;
        public int TaskCount => currentTaskUI != null ? currentTaskUI.tasks.Count : 0;
        public float OperationPenaltyPercent => Metrics != null ? Metrics.ScorePenaltyPercent : 0f;
        public float RepeatedOperationPercent => Metrics != null ? Metrics.ReworkPercent : 0f;
        public float OutsideOperationPercent => Metrics != null ? Metrics.OutOfAreaPercent : 0f;
        public float WastedSeeds => Metrics != null ? Metrics.WastedSeeds : 0f;
        public float WastedSprayLiters => Metrics != null ? Metrics.WastedSprayLiters : 0f;
        public float WastedFuelLiters => Metrics != null ? Metrics.WastedFuelLiters : 0f;
        public float CorrectPercentage => TrainingScoreCalculator.CalculatePercent(totalTasks, completedTasks, OperationPenaltyPercent);
        public int CompletedTasks => completedTasks;
        public int TotalTasks => totalTasks;
        public int ErrorCount => errorCount;
        public int TotalAttempts => completedTasks + errorCount;
        public float ElapsedTime => elapsedTime;
        public int WorkedCells => 0;
        public int WastedCells => 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (DateText != null) DateText.text = DateTime.Now.ToString("MMM d, yyyy");
            if (lessonAdapter == null)
                Debug.LogWarning("Training lesson adapter is not assigned. Assign a component that implements the TrainingReports.Training interfaces.", this);
        }

        private void Update()
        {
            if (!isRunning) return;
            elapsedTime += Time.deltaTime;
            if (TimerText != null) TimerText.text = ElapsedTimeFormatted;
        }

        public string ElapsedTimeFormatted
        {
            get
            {
                int hours = Mathf.FloorToInt(elapsedTime / 3600f);
                int minutes = Mathf.FloorToInt((elapsedTime % 3600f) / 60f);
                int seconds = Mathf.FloorToInt(elapsedTime % 60f);
                return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
            }
        }

        public void StartLessonTraining()
        {
            TaskCompleted = false;
            Task3HarvestingCompleted = false;
            Task3HarvestingSuccess = false;
            IsVehicleOutside = false;
            StartTrainingFromContext();
        }

        public void StartTrainingFromContext()
        {
            if (Context == null)
            {
                Debug.LogError("The assigned lesson adapter must implement ITrainingContext.", this);
                return;
            }
            StartTraining(Context.Operation);
        }

        public void StartTraining(TrainingOperation operation)
        {
            if (OperationRunner == null)
            {
                Debug.LogError("The assigned lesson adapter must implement ITrainingOperationRunner.", this);
                return;
            }

            StopLessonTraining();
            TaskResults.Clear();
            taskMessages.Clear();
            completedTasks = 0;
            errorCount = 0;
            elapsedTime = 0f;
            SetPanelsActive(operation);
            currentTaskUI = GetTaskUI(operation);
            if (currentTaskUI == null)
            {
                Debug.LogError("A task panel with TrainingTaskUI is required for the selected operation.", this);
                return;
            }

            currentTaskUI.TaskFinished -= OnTaskFinished;
            currentTaskUI.TaskFinished += OnTaskFinished;
            ITrainingTheme theme = lessonAdapter as ITrainingTheme;
            if (theme != null) currentTaskUI.Theme = theme;
            currentTaskUI.ResetTasks();
            totalTasks = currentTaskUI.tasks.Count;
            if (Report != null) Report.SetActive(false);
            StartTimer();
            isRunning = true;
            operationCoroutine = StartCoroutine(RunOperation(operation));
        }

        private IEnumerator RunOperation(TrainingOperation operation)
        {
            yield return OperationRunner.RunOperation(operation, Vehicle, this);
            operationCoroutine = null;
            isRunning = false;
            OpenReportPanel();
        }

        private void SetPanelsActive(TrainingOperation operation)
        {
            if (TractorTrainingUIObject != null) TractorTrainingUIObject.SetActive(operation == TrainingOperation.Tractor || operation == TrainingOperation.MechanizedWalk);
            if (PlantingTrainingUIObject != null) PlantingTrainingUIObject.SetActive(operation == TrainingOperation.Planting);
            if (SprayingTrainingUIObject != null) SprayingTrainingUIObject.SetActive(operation == TrainingOperation.Spraying);
            if (HarvestingTrainingUIObject != null) HarvestingTrainingUIObject.SetActive(operation == TrainingOperation.Harvesting || operation == TrainingOperation.HarvestingCorn);
        }

        private TrainingTaskUI GetTaskUI(TrainingOperation operation)
        {
            GameObject panel = null;
            switch (operation)
            {
                case TrainingOperation.Planting: panel = PlantingTrainingUIObject; break;
                case TrainingOperation.Spraying: panel = SprayingTrainingUIObject; break;
                case TrainingOperation.Harvesting:
                case TrainingOperation.HarvestingCorn: panel = HarvestingTrainingUIObject; break;
                default: panel = TractorTrainingUIObject; break;
            }
            return panel != null ? panel.GetComponentInChildren<TrainingTaskUI>(true) : null;
        }

        public void StopLessonTraining()
        {
            isRunning = false;
            if (currentTaskUI != null)
            {
                currentTaskUI.TaskFinished -= OnTaskFinished;
                currentTaskUI = null;
            }
            if (operationCoroutine != null)
            {
                StopCoroutine(operationCoroutine);
                operationCoroutine = null;
            }
            if (OperationRunner != null) OperationRunner.StopOperation();
            if (SprayingTrainingUIObject != null) SprayingTrainingUIObject.SetActive(false);
            if (HarvestingTrainingUIObject != null) HarvestingTrainingUIObject.SetActive(false);
            if (TractorTrainingUIObject != null) TractorTrainingUIObject.SetActive(false);
            if (PlantingTrainingUIObject != null) PlantingTrainingUIObject.SetActive(false);
        }

        public void StartTimer()
        {
            elapsedTime = 0f;
            isRunning = true;
        }

        public void SetTaskInProgress(int index)
        {
            if (currentTaskUI != null) currentTaskUI.SetTaskInProgress(index);
        }

        public void CompleteTask(int index, bool succeeded, string message)
        {
            taskMessages[index] = message;
            if (currentTaskUI != null) currentTaskUI.SetTaskCompleted(index, succeeded);
            else RegisterTaskResult(index, succeeded, message);
        }

        public void RegisterTaskResult(int index, bool success, string message)
        {
            string taskName = currentTaskUI != null && index >= 0 && index < currentTaskUI.tasks.Count && currentTaskUI.tasks[index].label != null
                ? currentTaskUI.tasks[index].label.text
                : string.Format("Task {0}", index + 1);
            TaskResults.Add(new TrainingTaskResult { TaskIndex = index, TaskName = taskName, Success = success, Message = message });
            if (success) completedTasks++;
            else errorCount++;
            UpdateReportUI();
        }

        public void OnTaskFinished(int index, bool success)
        {
            taskMessages.TryGetValue(index, out string message);
            taskMessages.Remove(index);
            RegisterTaskResult(index, success, message);
        }

        public void CompleteGarageTask() { TaskCompleted = true; }
        public void CompleteTruckTask() { Task3HarvestingCompleted = true; }

        private void OpenReportPanel()
        {
            SetPanelsActive((TrainingOperation)(-1));
            UpdateReportUI();
            if (ReportPresenter != null) ReportPresenter.ShowReport(BuildReport());
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (Report != null) Report.SetActive(true);
            StartCoroutine(FocusReportButtonRoutine());
        }

        private TrainingReportInput BuildReport()
        {
            ITrainingContext context = Context;
            var report = new TrainingReportInput
            {
                Operation = context != null ? context.Operation : TrainingOperation.Tractor,
                Crop = context != null ? context.Crop : string.Empty,
                FarmName = context != null ? context.FarmName : string.Empty,
                SceneName = context != null ? context.SceneName : string.Empty,
                StudentName = context != null ? context.StudentName : string.Empty,
                InstructorName = context != null ? context.InstructorName : string.Empty,
                VehicleModel = context != null ? context.VehicleModel : string.Empty,
                CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Duration = ElapsedTimeFormatted,
                ScorePercent = CorrectPercentage,
                ReworkPercent = RepeatedOperationPercent,
                OutOfAreaPercent = OutsideOperationPercent,
                ScorePenaltyPercent = OperationPenaltyPercent,
                WastedSeeds = WastedSeeds,
                WastedSprayLiters = WastedSprayLiters,
                WastedFuelLiters = WastedFuelLiters,
                EfficiencyPercent = Metrics != null ? Metrics.EfficiencyPercent : 0f
            };
            for (int i = 0; i < TaskResults.Count; i++)
            {
                TrainingTaskResult result = TaskResults[i];
                report.Tasks.Add(new TrainingReportTask
                {
                    TaskIndex = result.TaskIndex,
                    Title = result.TaskName,
                    Result = result.Message,
                    Passed = result.Success
                });
            }
            return report;
        }

        private IEnumerator FocusReportButtonRoutine()
        {
            yield return new WaitForEndOfFrame();
            FocusUIElement(ReportFirstButtonToFocus);
        }

        private void UpdateReportUI()
        {
            if (NumberErrorsText != null) NumberErrorsText.text = errorCount.ToString();
            if (PercentageCorrectText != null) PercentageCorrectText.text = string.Format("{0}%", Mathf.RoundToInt(CorrectPercentage));
        }

        private static void FocusUIElement(GameObject element)
        {
            if (EventSystem.current == null || element == null || !element.activeInHierarchy) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(element);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (currentTaskUI != null) currentTaskUI.TaskFinished -= OnTaskFinished;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
