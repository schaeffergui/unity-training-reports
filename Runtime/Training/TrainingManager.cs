using Rewired;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Primitives;
using VehiclePhysics;
using static RDSceneManager;

public class TrainingManager : MonoBehaviour
{
    public static TrainingManager Instance;

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

    public TextMeshProUGUI DateText;
    public TextMeshProUGUI TimerText;
    public TextMeshProUGUI TrainingText;
    public TextMeshProUGUI NumberErrorsText;
    public TextMeshProUGUI PercentageCorrectText;
    public bool IsVehicleOutside = false;
    public bool TaskCompleted = false;
    public bool Task3HarvestingCompleted = false;
    public bool Task3HarvestingSuccess = false;

    private TrainingTaskUI currentTaskUI;
    private LessonPdfExporter pdfExporter;
    private float elapsedTime;
    private bool isRunning = false;
    private bool isEngineWorking = false;
    private bool theMixerWasSwitchedOnAtTheRightTime = false;
    private bool ArmsOpenedInsideBuilding = false;
    private bool EnteredTheBarnWithOpenArms = false;
    private bool unloadedTheCargoOutsideTheTruck = false;
    private bool cutAboveTheRequiredHeight = false;
    private int totalTasks = 0;
    private int completedTasks = 0;
    private int errorCount = 0;
    private bool lessonTraining;
    private PlantingZoneTracker operationTracker;

    public bool IsTrainingRunning => isRunning;
    public float OperationPenaltyPercent => operationTracker != null ? operationTracker.PenaltyPercent : 0f;
    public float RepeatedOperationPercent => operationTracker != null ? operationTracker.RepeatedPercent : 0f;
    public float OutsideOperationPercent => operationTracker != null ? operationTracker.OutsidePercent : 0f;
    public float WastedSeeds => operationTracker != null ? operationTracker.WastedSeeds : 0f;
    public float WastedSprayLiters => operationTracker != null ? operationTracker.WastedSprayLiters : 0f;
    public float WastedFuelLiters => operationTracker != null ? operationTracker.WastedFuelLiters : 0f;
    public int WorkedCells => operationTracker != null ? operationTracker.WorkedCells : 0;
    public int WastedCells => operationTracker != null ? operationTracker.RepeatedCells + operationTracker.OutsideCells : 0;

    public void StartLessonTraining()
    {
        StopAllCoroutines();
        TaskCompleted = false;
        Task3HarvestingCompleted = false;
        Task3HarvestingSuccess = false;
        IsVehicleOutside = false;
        ArmsOpenedInsideBuilding = false;
        EnteredTheBarnWithOpenArms = false;
        lessonTraining = true;
        StartTraining(RDSceneManager.Operation);
    }

    public void StopLessonTraining()
    {
        if (!lessonTraining) return;
        lessonTraining = false;
        isRunning = false;
        StopAllCoroutines();
        if (pdfExporter != null) pdfExporter.StopLessonTelemetryCollection();
        if (SprayingTrainingUIObject != null) SprayingTrainingUIObject.SetActive(false);
        if (HarvestingTrainingUIObject != null) HarvestingTrainingUIObject.SetActive(false);
        if (TractorTrainingUIObject != null) TractorTrainingUIObject.SetActive(false);
        if (PlantingTrainingUIObject != null) PlantingTrainingUIObject.SetActive(false);
    }

    [Header("UI Focus")]
    [Tooltip("The button that should be focused when the Report panel opens.")]
    public GameObject ReportFirstButtonToFocus;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        DateTime today = DateTime.Now;
        DateText.text = today.ToString("dd/MM/yyyy");

        pdfExporter = FindObjectOfType<LessonPdfExporter>();
        if (pdfExporter == null)
        {
            Debug.LogWarning("LessonPdfExporter not found. Creating a new one...");
            GameObject exporterGO = new GameObject("LessonPdfExporter");
            pdfExporter = exporterGO.AddComponent<LessonPdfExporter>();
        }
    }

    private void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;

            int hours = Mathf.FloorToInt(elapsedTime / 3600);
            int minutes = Mathf.FloorToInt((elapsedTime % 3600) / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);

            TimerText.text = $"{hours:00}:{minutes:00}:{seconds:00}";

        }
    }
    [System.Serializable]
    public class TrainingTaskResult
    {
        public int TaskIndex;
        public string TaskName;
        public bool Success;
        public string Message;
    }

    public void StartTimer()
    {
        isRunning = true;
        elapsedTime = 0f;
    }

    public string ElapsedTimeFormatted
    {
        get
        {
            int hours = Mathf.FloorToInt(elapsedTime / 3600);
            int minutes = Mathf.FloorToInt((elapsedTime % 3600) / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }
    }

    public void StartTraining(RDSceneManager.Operations operation)
    {
        TaskResults.Clear();
        operationTracker = FindFirstObjectByType<PlantingZoneTracker>();
        operationTracker?.ResetTracking();

        if (pdfExporter != null)
        {
            pdfExporter.StartLessonTelemetryCollection();
        }

        // Disable all training UIs on start
        if (TractorTrainingUIObject != null) TractorTrainingUIObject.SetActive(false);
        if (PlantingTrainingUIObject != null) PlantingTrainingUIObject.SetActive(false);
        if (SprayingTrainingUIObject != null) SprayingTrainingUIObject.SetActive(false);
        if (HarvestingTrainingUIObject != null) HarvestingTrainingUIObject.SetActive(false);

        TrainingTaskUI activeTaskUI = null;

        switch (operation)
        {
            case RDSceneManager.Operations.Tractor:
                if (TractorTrainingUIObject != null)
                {
                    TractorTrainingUIObject.SetActive(true);
                    activeTaskUI = TractorTrainingUIObject.GetComponentInChildren<TrainingTaskUI>(true);
                    OpenWoodenGate.SetActive(true);
                    TractorPath.SetActive(true);

                    foreach (GameObject cylinder in TractorCylinderGameObjects)
                    {
                        if (cylinder != TractorCylinderGameObjects[0])
                        {
                            cylinder.SetActive(false);
                        }
                    }
                }
                break;

            case RDSceneManager.Operations.Planting:
                if (PlantingTrainingUIObject != null)
                {
                    PlantingTrainingUIObject.SetActive(true);
                    activeTaskUI = PlantingTrainingUIObject.GetComponentInChildren<TrainingTaskUI>(true);
                    ClosedWoodenGate.SetActive(true);
                }
                break;

            case RDSceneManager.Operations.Spraying:
                if (SprayingTrainingUIObject != null)
                {
                    SprayingTrainingUIObject.SetActive(true);
                    activeTaskUI = SprayingTrainingUIObject.GetComponentInChildren<TrainingTaskUI>(true);
                    ClosedWoodenGate.SetActive(true);
                }
                break;

            case RDSceneManager.Operations.Harvesting:
            case RDSceneManager.Operations.HarvertingCorn:
                if (HarvestingTrainingUIObject != null)
                {
                    HarvestingTrainingUIObject.SetActive(true);
                    activeTaskUI = HarvestingTrainingUIObject.GetComponentInChildren<TrainingTaskUI>(true);
                    ClosedWoodenGate.SetActive(true);
                }
                break;
        }

        currentTaskUI = activeTaskUI;
        totalTasks = 0;
        errorCount = 0;
        completedTasks = 0;
        if (currentTaskUI != null)
        {
            totalTasks = currentTaskUI.tasks.Count;
        }

        switch (operation)
        {
            case RDSceneManager.Operations.Tractor: StartCoroutine(TractorTraining(activeTaskUI)); break;
            case RDSceneManager.Operations.Planting: StartCoroutine(PlantingTraining(activeTaskUI)); break;
            case RDSceneManager.Operations.Spraying: StartCoroutine(SprayingTraining(activeTaskUI)); break;
            case RDSceneManager.Operations.Harvesting:
            case RDSceneManager.Operations.HarvertingCorn: StartCoroutine(HarvestingTraining(activeTaskUI)); break;
            case RDSceneManager.Operations.MechanizedWalk: StartCoroutine(MechanizedWalkTraining(activeTaskUI)); break;
        }
    }

    private IEnumerator TractorTraining(TrainingTaskUI taskUI)
    {
        StartTimer();
        TrainingText.text = "Tractor on the road";
        GameObject planterGO = null;
        GameObject machineBody = null;

        while (planterGO == null && machineBody == null)
        {
            planterGO = GameObject.Find("pes");
            machineBody = GameObject.Find("Machine_8_Tractor_A(Clone)/Base_Tractor_A/Body");

            if (machineBody != null && machineBody.GetComponent<RDColliderController>() == null)
                machineBody.AddComponent<RDColliderController>();

            yield return new WaitForSeconds(0.5f);
        }

        if (planterGO != null && planterGO.GetComponent<RDColliderController>() == null)
            planterGO.AddComponent<RDColliderController>();

        RDTractorController tractor = null;
        while (tractor == null)
        {
            tractor = FindObjectOfType<RDTractorController>();
            yield return new WaitForSeconds(0.5f);
        }

        if (TractorBackCollider != null)
        {
            TractorBackCollider.transform.SetParent(tractor.transform);
            TractorBackCollider.transform.localPosition = new Vector3(0f, 0.8f, -2.8f);
        }

        #region Task 0: Start the tractor
        taskUI?.SetTaskInProgress(0);

        while (RDVehicles.Player == null || !RDVehicles.Player.EngineWorking)
            yield return new WaitForSeconds(1f);

        taskUI?.SetTaskCompleted(0, true);
        RegisterTaskResult(0, true, "The tractor was started correctly.");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 1: Follow the Track
        taskUI?.SetTaskInProgress(1);

        for (int i = 0; i < TractorCylinderGameObjects.Length; i++)
        {
            if (TractorCylinderGameObjects[i] != null)
            {
                TractorCylinderGameObjects[i].SetActive(i == 0);
            }
        }

        yield return new WaitUntil(() => TractorCylinderGameObjects[0] == null || !TractorCylinderGameObjects[0].activeSelf);

        for (int i = 1; i < TractorCylinderGameObjects.Length; i++)
        {
            if (TractorCylinderGameObjects[i] != null)
            {
                TractorCylinderGameObjects[i].SetActive(true);
                yield return new WaitUntil(() => TractorCylinderGameObjects[i] == null || !TractorCylinderGameObjects[i].activeSelf);
            }
        }

        while (!TaskCompleted)
        {
            yield return new WaitForSeconds(0.1f);
        }

        isRunning = false;
        taskUI?.SetTaskCompleted(1, true);
        RegisterTaskResult(1, true, "Plantadeira acoplada corretamente.");
        yield return new WaitForSeconds(1f);
        #endregion

        OpenReportPanel();
    }

    private IEnumerator PlantingTraining(TrainingTaskUI taskUI)
    {
        StartTimer();
        TrainingText.text = "Planting";
        RDTractorController tractor = null;
        GameObject machineBody = null;
        GameObject machineSeeder = null;

        while (tractor == null && machineBody == null && machineSeeder == null)
        {
            tractor = FindObjectOfType<RDTractorController>();
            machineBody = GameObject.Find("Machine_275_Tractor_A_Planter(Clone)/Base_Tractor_A/Body");
            machineSeeder = GameObject.Find("Machine_275_Tractor_A_Planter(Clone)/PlantadeiraGirada/Plantadeira");

            if (machineBody != null && machineBody.GetComponent<RDColliderController>() == null)
            machineBody.AddComponent<RDColliderController>();
            if (machineSeeder != null && machineSeeder.GetComponent<RDColliderController>() == null)
            machineSeeder.AddComponent<RDColliderController>();

            yield return new WaitForSeconds(0.5f);
        }

        RDPlanterController planter = FindObjectOfType<RDPlanterController>();
        operationTracker?.StartTracking(tractor, planter);

        #region Task 0: Start the tractor
        taskUI?.SetTaskInProgress(0);
        while (RDVehicles.Player == null || !RDVehicles.Player.EngineWorking)
            yield return new WaitForSeconds(1f);

        taskUI?.SetTaskCompleted(0, true);
        RegisterTaskResult(0, true, "The tractor was started correctly.");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 1: Lift the implement
        taskUI?.SetTaskInProgress(1);
        bool task1Success = true;
        while (planter.HydraulicHeight <= 1.214f)
        {
            if (StopIfMachineIsDestroyed(null, planter, null)) yield break;
            if (RDInput.GetButton(RewiredConsts.Action.Throttle)) task1Success = false;
            yield return new WaitForSeconds(0.1f);
        }
        taskUI?.SetTaskCompleted(1, task1Success);
        RegisterTaskResult(1, task1Success, task1Success ? "Implement raised correctly." : "Improper acceleration while raising the implement.");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 2: Carry out the planting 
        taskUI?.SetTaskInProgress(2);
        PlantingZoneTracker tracker = operationTracker;

        while (tracker != null && tracker.GetProgressPercent() < 100f)
        {
            if (StopIfMachineIsDestroyed(null, planter, null)) yield break;
            taskUI?.UpdateTaskProgress(2);
            if (tracker.GetProgressPercent() > 42f)
            {
                taskUI?.SetTaskCompleted(2, true);
                RegisterTaskResult(2, true, "Planting completed successfully.");
                Garage.SetActive(true);
                break;
            }
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 3: Lift the implement after planting
        taskUI.SetTaskInProgress(3);
        bool task3Success = true;
        while (planter.HydraulicHeight <= 1.214f)
        {
            if (StopIfMachineIsDestroyed(null, planter, null)) yield break;
            if (RDInput.GetButton(RewiredConsts.Action.Throttle)) task3Success = false;
            yield return new WaitForSeconds(0.1f);
        }
        taskUI.SetTaskCompleted(3, task3Success);
        RegisterTaskResult(3, task3Success, task3Success ? "Implement retracted successfully." : "Improper movement while raising the implement");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 4: Take the tractor back to the garage 
        taskUI?.SetTaskInProgress(4);
        TaskCompleted = false;
        while (!TaskCompleted)
        {
            if (StopIfMachineIsDestroyed(null, planter, null)) yield break;
            yield return new WaitForSeconds(0.5f);
        }
        taskUI?.SetTaskCompleted(4, true);
        RegisterTaskResult(4, true, "Vehicle stored in the garage.");
        #endregion

        isRunning = false;
        OpenReportPanel();
    }

    private IEnumerator SprayingTraining(TrainingTaskUI taskUI)
    {
        StartTimer();
        TrainingText.text = "Spraying";
        RDSprayerController sprayer = null;
        GameObject machineBody = null;
        GameObject machineArmL = null;
        GameObject machineArmR = null;

        bool setTask = false;
        bool setTask1 = false;
        bool setTask2 = false;
        bool setTask3 = false;
        bool setTask4 = false;


        while (sprayer == null && machineBody == null && machineArmL == null && machineArmR == null)
        {
            sprayer = FindObjectOfType<RDSprayerController>();
            machineBody = GameObject.Find("Machine_9_Sprayer(Clone)/VehicleModel/Body");
            machineArmL = GameObject.Find("Machine_9_Sprayer(Clone)/VehicleModel/BraçoL");
            machineArmR = GameObject.Find("Machine_9_Sprayer(Clone)/VehicleModel/BraçoR");

            machineBody.AddComponent<RDColliderController>();
            machineArmL.AddComponent<RDColliderController>();
            machineArmR.AddComponent<RDColliderController>();
            yield return new WaitForSeconds(0.5f);
        }

        operationTracker?.StartTrackingSpraying(sprayer);

        #region Task 0: Turn on the Sprayer (Motor → Bomba → Misturador)
        taskUI?.SetTaskInProgress(0);

        bool taskSuccess = false;
        string failReason = "";

        while (!RDVehicles.Player.EngineWorking)
        {
            if (sprayer.IsPumpOn || sprayer.IsMixerOn)
            {
                failReason = "Pump or mixer switched on before the engine.";
                break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(failReason) && RDVehicles.Player.EngineWorking)
        {
            while (!sprayer.IsPumpOn)
            {
                if (sprayer.IsMixerOn)
                {
                    failReason = "Mixer switched on before the pump.";
                    break;
                }
                if (!RDVehicles.Player.EngineWorking)
                {
                    failReason = "Engine off before switching on the pump.";
                    break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (string.IsNullOrEmpty(failReason) && sprayer.IsPumpOn)
        {
            while (!sprayer.IsMixerOn)
            {
                if (!sprayer.IsPumpOn)
                {
                    failReason = "Bomba desligada antes de ligar o misturador.";
                    break;
                }
                if (!RDVehicles.Player.EngineWorking)
                {
                    failReason = "Engine off before switching on the mixer.";
                    break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        taskSuccess = string.IsNullOrEmpty(failReason) && sprayer.IsMixerOn;

        taskUI?.SetTaskCompleted(0, taskSuccess);
        TrainingManager.Instance.RegisterTaskResult(0, taskSuccess, taskSuccess
            ? "The engine was started correctly, following the proper procedure (engine → pump → mixer)."
            : $"Falha: {failReason}");

        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 1: Open the arms outside the barn
        taskUI?.SetTaskInProgress(1);

        while (!IsVehicleOutside)
        {
            if (StopIfMachineIsDestroyed(sprayer, null, null))
                yield break;

            if (sprayer.ArmsOpened)
            {
                ArmsOpenedInsideBuilding = true;
            }
            yield return new WaitForSeconds(1f);
        }

        if (!ArmsOpenedInsideBuilding)
        {
            setTask1 = true;
        }
        else
        {
            setTask1 = false;
        }
        taskUI.SetTaskCompleted(1, setTask1);

        TrainingManager.Instance.RegisterTaskResult(1, setTask1, setTask1
        ? "The arms were opened correctly, following the proper procedure."
        : "The arms were not opened according to the correct procedure.");

        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 2: Spray the fields
        taskUI?.SetTaskInProgress(2);
        PlantingZoneTracker tracker = operationTracker;
        if (tracker == null)
        {
            yield break;
        }

        while (tracker.GetProgressPercent() < 100f)
        {
            if (StopIfMachineIsDestroyed(sprayer, null, null))
                yield break;

            float progress = tracker.GetProgressPercent();
            //Debug.Log($"[Task 2] Planting progress: {progress:F1}%");
            taskUI?.UpdateTaskProgress(2);

            if (progress > 33f)
            {
                Garage.SetActive(true);
                setTask2 = true;
                break;
            }

            yield return new WaitForSeconds(0.5f);
        }
        taskUI?.SetTaskCompleted(2, setTask2);

        TrainingManager.Instance.RegisterTaskResult(2, setTask2, setTask2
        ? "Spraying performed correctly."
        : "Spraying performed incorrectly.");

        #endregion

        #region Task 3: Close the arms outside the barn
        taskUI?.SetTaskInProgress(3);

        while (sprayer.ArmsOpened)
        {
            if (StopIfMachineIsDestroyed(sprayer, null, null))
                yield break;

            if (!IsVehicleOutside)
            {
                EnteredTheBarnWithOpenArms = true;
            }
            yield return new WaitForSeconds(1f);
        }

        if (!EnteredTheBarnWithOpenArms)
        {
            setTask3 = true;
        }
        else
        {
            setTask3 = false;
        }
        taskUI?.SetTaskCompleted(3, setTask3);
        TrainingManager.Instance.RegisterTaskResult(3, setTask3, setTask3
        ? "The arms were closed correctly, following the proper procedure."
        : "The arms were not closed according to the correct procedure.");

        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 4: Store the machinery after spraying
        taskUI?.SetTaskInProgress(4);
        GarageArea garageArea = Garage.GetComponent<GarageArea>();

        while (garageArea == null)
        {
            garageArea = Garage.GetComponent<GarageArea>();
        }

        // Task 4: Take the tractor back to the garage 
        while (!TaskCompleted)
        {
            if (StopIfMachineIsDestroyed(sprayer, null, null))
                yield break;
            yield return new WaitForSeconds(1f);
            setTask4 = true;
        }
        taskUI?.SetTaskCompleted(4, setTask4);

        TrainingManager.Instance.RegisterTaskResult(4, setTask4, setTask4
        ? "The vehicle was positioned correctly in the storage area."
        : "The vehicle was not positioned correctly in the storage area.");
        #endregion

        isRunning = false;

        OpenReportPanel();
    }

    private IEnumerator HarvestingTraining(TrainingTaskUI taskUI)
    {
        StartTimer();
        TrainingText.text = "Colheitadeira";
        RDHarvesterController harvester = null;
        RDHarvestClassController rdHarvestClassController = null;
        GameObject machineBody = null;
        GameObject machineImplement = null;
        GameObject machineReelCutter = null;

        while (harvester == null || rdHarvestClassController == null || machineBody == null || machineImplement == null || machineReelCutter == null)
        {
            harvester = FindObjectOfType<RDHarvesterController>();
            rdHarvestClassController = FindObjectOfType<RDHarvestClassController>();

            if (RDSceneManager.Operation == RDSceneManager.Operations.Harvesting)
            {
                GameObject wheatBase = harvester != null ? harvester.gameObject : null;

                if (wheatBase != null)
                {
                    machineBody = wheatBase.transform.Find("VehicleModel/Harvester")?.gameObject;
                    machineImplement = wheatBase.transform.Find("Harvester_Wheat/Harvester_Wheat")?.gameObject;
                    machineReelCutter = wheatBase.transform.Find("Harvester_Wheat/Harvester_Wheat_Disk")?.gameObject;
                }
            }
            else if (RDSceneManager.Operation == RDSceneManager.Operations.HarvertingCorn)
            {
                GameObject cornBase = harvester != null ? harvester.gameObject : null;

                if (cornBase != null)
                {
                    machineBody = cornBase.transform.Find("VehicleModel/Harvester")?.gameObject;
                    machineImplement = cornBase.transform.Find("Harvester_Corn/Harvester_Corn")?.gameObject;
                    machineReelCutter = cornBase.transform.Find("Harvester_Corn/Harvester_Corn_Disk")?.gameObject;
                }
            }

            if (machineBody != null && machineBody.GetComponent<RDColliderController>() == null)
                machineBody.AddComponent<RDColliderController>();

            if (machineImplement != null && machineImplement.GetComponent<RDColliderController>() == null)
                machineImplement.AddComponent<RDColliderController>();

            if (machineReelCutter != null && machineReelCutter.GetComponent<RDColliderController>() == null)
                machineReelCutter.AddComponent<RDColliderController>();

            yield return new WaitForSeconds(0.5f);
        }

        #region Task 0: Start the Vehicle
        taskUI?.SetTaskInProgress(0);

        operationTracker?.StartTrackingHarvester(harvester);
        int treesRemovedCount = harvester.TreesRemovedCount;
        float targetHeight = RDSceneManager.HarvestHeightUI(RDSceneManager.Crop) * 1000f ;
        taskUI?.SetTargetHeight(1, targetHeight);

        while (RDVehicles.Player == null || !RDVehicles.Player.EngineWorking)
        {
            yield return new WaitForSeconds(1f);
        }
        taskUI?.SetTaskCompleted(0, true);
        RegisterTaskResult(0, true, "Vehicle started correctly.");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 1: Adjust the height of the reel
        taskUI?.SetTaskInProgress(1);
        taskUI?.SetTargetHeight(1, targetHeight);
        float targetHarvestHeight = RDSceneManager.GetHarvestHeightForCrop(RDSceneManager.Crop);
        float tolerance = 0.1f;
        //Debug.Log($"[HarvestingTraining] Target height for {RDSceneManager.Crop}: {targetHarvestHeight:F2}");

        while (Mathf.Abs(harvester.CombinHeight - targetHarvestHeight + 0.1f) > tolerance)
        {
            if (StopIfMachineIsDestroyed(null, null, harvester))
                yield break;
            //Debug.Log($"Altura atual: {harvester.CombinHeight:F2} | Alvo: {targetHarvestHeight:F2} | Diferen�a: {Mathf.Abs(harvester.CombinHeight - targetHarvestHeight):F2}");
            yield return new WaitForSeconds(0.1f);
        }
        taskUI?.SetTaskCompleted(1, true);
        RegisterTaskResult(1, true, "Reel height adjusted correctly.");
        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 2: Carry out the harvest
        taskUI?.SetTaskInProgress(2);

        bool cutAboveTheRequiredHeight = false;
        bool success = true;

        while (harvester.TreesRemovedCount <= 4000)
        {
            if (StopIfMachineIsDestroyed(null, null, harvester))
                yield break;

            if (treesRemovedCount != harvester.TreesRemovedCount)
            {
                if (harvester.CombinHeight > targetHeight)
                {
                    cutAboveTheRequiredHeight = true;
                    success = false;
                }

                treesRemovedCount = harvester.TreesRemovedCount;
            }

            yield return new WaitForSeconds(0.1f);
        }

        success = !cutAboveTheRequiredHeight;

        taskUI?.SetTaskCompleted(2, success);

        TrainingManager.Instance.RegisterTaskResult(2,
            success,
            success
                ? "Harvesting was done correctly, respecting the recommended height."
                : "Harvesting was done above the recommended height, compromising operation quality."
        );

        Truck.SetActive(true);

        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 3: Turn off the reel

        taskUI?.SetTaskInProgress(3);

        while (harvester.IsRotating && !Task3HarvestingCompleted)
        {
            if (StopIfMachineIsDestroyed(null, null, harvester))
                yield break;

            yield return null;
        }

        Task3HarvestingSuccess = !harvester.IsRotating;

        taskUI?.SetTaskCompleted(3, Task3HarvestingSuccess);

        RegisterTaskResult(
            3,
            Task3HarvestingSuccess,
            Task3HarvestingSuccess
                ? "The reel was switched off correctly."
                : "Failure: The reel was not switched off before approaching the truck."
        );

        #endregion

        #region Task 4: Unload the cargo on the truck
        taskUI?.SetTaskInProgress(4);

        yield return new WaitUntil(() => harvester.TreesRemovedCount > 0);

        while (harvester.TreesRemovedCount > 0)
        {
            if (StopIfMachineIsDestroyed(null, null, harvester)) yield break;
            yield return null;
        }

        bool task4Success = (harvester.TreesRemovedCount == 0);


        taskUI?.SetTaskCompleted(4, task4Success);

        RegisterTaskResult(4, task4Success, task4Success
            ? "Unloading completed successfully."
            : "Unloading was not completed correctly.");

        if (Garage != null)
        {
            Garage.SetActive(true);
        }

        yield return new WaitForSeconds(1f);
        #endregion

        #region Task 5: Store the machinery after unloading
        taskUI?.SetTaskInProgress(5);

        TaskCompleted = false;

        while (!TaskCompleted)
        {
            if (StopIfMachineIsDestroyed(null, null, harvester)) yield break;
            yield return null;
        }

        bool task5Success = !harvester.PipeOpened;

        taskUI?.SetTaskCompleted(5, task5Success);

        RegisterTaskResult(5, task5Success, task5Success
            ? "Machine stored successfully with the pipe closed."
            : "Machine was not stored correctly. You entered the garage with the unloading pipe open");

        #endregion

        isRunning = false;
        OpenReportPanel();
    }

    private void OpenReportPanel()
    {
        if (pdfExporter != null)
        {
            pdfExporter.StopLessonTelemetryCollection();
        }

        if (SprayingTrainingUIObject) SprayingTrainingUIObject.SetActive(false);
        if (HarvestingTrainingUIObject) HarvestingTrainingUIObject.SetActive(false);
        if (TractorTrainingUIObject) TractorTrainingUIObject.SetActive(false);
        if (PlantingTrainingUIObject) PlantingTrainingUIObject?.SetActive(false);

        if (lessonTraining)
        {
            lessonTraining = false;
            return;
        }

        UpdateReportUI();
        var reportController = RDALessonReportController.Instance;
        if (reportController != null)
        {
            if (TrainingText != null && reportController.currentLesson != null)
            {
                TrainingText.text = reportController.currentLesson.Tittle;
            }
            reportController.FinishLesson(LessonStudentPerformance.None);
        }
        else
        {
            Debug.LogError("Could not generate the report: RDALessonReportController is not available.");
        }

        if (RDVehicles.Player != null)
        {
            RDVehicles.Player.CanDrive = false;
            RDVehicles.Player.Handbrake = 1f;
            RDVehicles.Player.Throttle = 0f;
            RDVehicles.Player.Ignition = RDVehicle.IgnitionKeyPosition.Off;
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;

        Report.SetActive(true);
        StartCoroutine(FocusReportButtonRoutine());
    }

    private IEnumerator FocusReportButtonRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (ReportFirstButtonToFocus != null)
        {
            FocusUIElement(ReportFirstButtonToFocus);
        }
    }

    public void RegisterTaskResult(int index, bool success, string message)
    {
        TaskResults.Add(new TrainingTaskResult
        {
            TaskIndex = index,
            Success = success,
            Message = message
        });

        if (success)
            completedTasks++;
        else
            errorCount++;
    }

    private bool StopIfMachineIsDestroyed(RDSprayerController sprayer, RDPlanterController planter, RDHarvesterController harvester)
    {
        if (sprayer != null && sprayer.Health <= 0)
        {
            isRunning = false;

            TrainingText.text = "Spraying";
            SprayingTrainingUIObject.SetActive(false);
            AudioListener.pause = true;
            Report.SetActive(true);

            return true;
        }
        else if (planter != null && planter.Health <= 0)
        {
            isRunning = false;

            TrainingText.text = "Planting";
            SprayingTrainingUIObject.SetActive(false);
            AudioListener.pause = true;
            Report.SetActive(true);

            return true;
        }
        else if (harvester != null && harvester.Health <= 0)
        {
            isRunning = false;

            TrainingText.text = "Colheitadeira";
            SprayingTrainingUIObject.SetActive(false);
            AudioListener.pause = true;
            Report.SetActive(true);

            return true;
        }
        return false;
    }

    public void CompleteGarageTask()
    {
        TaskCompleted = true;
    }

    public void CompleteTruckTask()
    {
        Task3HarvestingCompleted = true;
    }

    public void OnTaskFinished(int index, bool success)
    {
        UpdateReportUI();
    }
    private void UpdateReportUI()
    {
        NumberErrorsText.text = errorCount.ToString();

        float percentage = CorrectPercentage;
        PercentageCorrectText.text = $"{Mathf.RoundToInt(percentage)}%";

    }

    private IEnumerator MechanizedWalkTraining(TrainingTaskUI taskUI)
    {
        yield return null;
    }

    private void FocusUIElement(GameObject elementToFocus)
    {
        if (EventSystem.current != null && elementToFocus != null && elementToFocus.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(elementToFocus);
        }
    }

    public float CorrectPercentage
    {
        get
        {
            if (totalTasks <= 0) return 0f;
            return Mathf.Clamp(((float)completedTasks / totalTasks) * 100f - OperationPenaltyPercent, 0f, 100f);
        }
    }

    public int CompletedTasks => completedTasks; 
    public int TotalTasks => totalTasks;
    public int ErrorCount => errorCount;
    public int TotalAttempts => completedTasks + errorCount;
    public float ElapsedTime => elapsedTime;
    public List<TrainingTaskResult> TaskResults = new List<TrainingTaskResult>();
}
