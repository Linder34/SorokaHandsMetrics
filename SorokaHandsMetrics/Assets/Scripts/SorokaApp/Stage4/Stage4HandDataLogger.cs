using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class Stage4HandDataLogger : MonoBehaviour {
    [Header("Subject")]
    [SerializeField] private int subjectId = 1;

    [Header("CSV Files")]
    [SerializeField] private string resultsFileName = "Stage4_results.csv";
    [SerializeField] private string trajectoryFileName = "Stage4_hand_trajectory.csv";

    [Header("Trajectory Sampling")]
    [SerializeField] private float trajectoryLogHz = 30f;

    [Header("Stage 4 References")]
    [SerializeField] private Stage4PathController pathController;
    [SerializeField] private Transform basketTransform;
    [SerializeField] private Stage4PlacementZone placementZone;

    [Header("Palm Openness")]
    [SerializeField] private float closedThreshold = 0.02f;
    [SerializeField] private float openThreshold = 0.15f;

    private string ResultsPath =>
        Path.Combine(Application.persistentDataPath, resultsFileName);

    private string TrajectoryPath =>
        Path.Combine(Application.persistentDataPath, trajectoryFileName);

    private Stage4Config config;
    private OVRSkeleton handSkeleton;

    private string sessionId;

    private Transform wrist;
    private Transform thumbTip;
    private Transform indexTip;
    private Transform middleTip;
    private Transform ringTip;
    private Transform pinkyTip;

    private bool bonesReady;

    private int currentCycleIndex;
    private string currentObjectName;
    private GameObject currentObject;

    private float cycleStartTime;
    private float nextTrajectoryLogTime;

    private float initialDistance;
    private float distanceAt30;
    private bool recorded30;

    private float maxOpenness;
    private float maxOpennessDistance;

    private Vector3 fruitStartPosition;

    private float actualPathLength;
    private Vector3 lastObjectPosition;
    private bool hasLastObjectPosition;

    private float directFruitBasketDistance;
    private float idealCenterlinePathLength;

    private float TrajectoryInterval =>
        trajectoryLogHz <= 0f
            ? 0f
            : 1f / trajectoryLogHz;

    private static readonly string[] ResultsHeaderColumns =
    {
        "RowType",
        "Subject",
        "SessionID",
        "UnixTime_s",
        "Stage",
        "HandSelection",
        "EndlessMode",
        "TrialsAmount",
        "UseTimeLimit",
        "TimeLimitSeconds",
        "TargetDistance",
        "TargetSize",
        "RingSize",
        "PathHeight",

        "CycleIndex",
        "ObjectName",
        "CycleResult",
        "TotalTime_s",
        "MaxContinuousGrab_s",
        "MaxOpenness_pct",
        "MaxOpennessDist_m",
        "InitialDistance_m",
        "DistancePalmOpened_m",

        "ActualPathLength_m",
        "IdealCenterlinePathLength_m",
        "DirectFruitBasketDistance_m",
        "FinalDistanceToBasket_m",

        "WrongCheckpointCount",
        "CheckpointsCompleted",
        "PathCompleted",

        "FruitStart_x",
        "FruitStart_y",
        "FruitStart_z",

        "BasketPos_x",
        "BasketPos_y",
        "BasketPos_z",

        "Ring1_x",
        "Ring1_y",
        "Ring1_z",

        "Ring2_x",
        "Ring2_y",
        "Ring2_z",

        "Ring3_x",
        "Ring3_y",
        "Ring3_z",

        "ObjectPos_x",
        "ObjectPos_y",
        "ObjectPos_z",

        "ObjectRot_x",
        "ObjectRot_y",
        "ObjectRot_z",
        "ObjectRot_w",

        "ObjectScale_x",
        "ObjectScale_y",
        "ObjectScale_z",

        "SessionScore",
        "SessionTotalCycles",
        "SessionSuccessRatePercent"
    };

    private static readonly string[] TrajectoryHeaderColumns =
    {
        "RowType",
        "Subject",
        "SessionID",
        "UnixTime_s",
        "Stage",
        "HandSelection",
        "EndlessMode",
        "TrialsAmount",
        "UseTimeLimit",
        "TimeLimitSeconds",
        "TargetDistance",
        "TargetSize",
        "RingSize",
        "PathHeight",

        "CycleIndex",
        "ObjectName",
        "tSinceCycleStart_s",
        "IsObjectGrabbed",
        "CurrentContinuousGrab_s",
        "Openness_pct",
        "ThumbIndexDistance_m",

        "ObjectPos_x",
        "ObjectPos_y",
        "ObjectPos_z",

        "BasketPos_x",
        "BasketPos_y",
        "BasketPos_z",

        "Ring1_x",
        "Ring1_y",
        "Ring1_z",

        "Ring2_x",
        "Ring2_y",
        "Ring2_z",

        "Ring3_x",
        "Ring3_y",
        "Ring3_z",

        "NextRequiredRing",
        "CheckpointsCompleted",
        "PathCompleted",
        "WrongCheckpointCount",
        "BasketObjectInside",

        "DistanceToCurrentRing_m",
        "DistanceToBasket_m",
        "ActualPathLengthSoFar_m",

        "Wrist_x",
        "Wrist_y",
        "Wrist_z",

        "ThumbTip_x",
        "ThumbTip_y",
        "ThumbTip_z",

        "IndexTip_x",
        "IndexTip_y",
        "IndexTip_z",

        "MiddleTip_x",
        "MiddleTip_y",
        "MiddleTip_z",

        "RingTip_x",
        "RingTip_y",
        "RingTip_z",

        "PinkyTip_x",
        "PinkyTip_y",
        "PinkyTip_z"
    };

    public void BeginSession(
        Stage4Config stageConfig,
        OVRSkeleton skeleton) {
        config = stageConfig;
        handSkeleton = skeleton;

        sessionId =
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        bonesReady = false;
        CacheBones();

        EnsureResultsHeader();
        EnsureTrajectoryHeader();

        WriteSettingsRows();

        FileLogger.Log(
            $"Stage4HandDataLogger: BeginSession sessionId={sessionId}");
    }

    public void BeginCycle(
        int cycleIndex,
        GameObject targetObject) {
        currentCycleIndex = cycleIndex;
        currentObject = targetObject;

        currentObjectName =
            targetObject != null
                ? targetObject.name
                : "null";

        cycleStartTime = Time.time;
        nextTrajectoryLogTime = 0f;

        recorded30 = false;
        distanceAt30 = 0f;

        float opennessDist;
        float openness =
            ComputePalmOpenness(out opennessDist);

        maxOpenness = openness;
        maxOpennessDistance = opennessDist;

        initialDistance = 0f;

        if (wrist != null && currentObject != null) {
            initialDistance =
                Vector3.Distance(
                    wrist.position,
                    currentObject.transform.position);
        }

        fruitStartPosition =
            currentObject != null
                ? currentObject.transform.position
                : Vector3.zero;

        actualPathLength = 0f;

        lastObjectPosition = fruitStartPosition;
        hasLastObjectPosition = currentObject != null;

        directFruitBasketDistance =
            basketTransform != null
                ? Vector3.Distance(
                    fruitStartPosition,
                    basketTransform.position)
                : 0f;

        idealCenterlinePathLength =
            ComputeIdealCenterlinePathLength();

        FileLogger.Log(
            $"Stage4HandDataLogger: BeginCycle " +
            $"cycle={cycleIndex}, object={currentObjectName}");
    }

    public void SampleTrajectory(
        bool isObjectGrabbed,
        float currentContinuousGrabSeconds) {
        if (config == null ||
            handSkeleton == null ||
            currentObject == null) {
            return;
        }

        float tSinceCycleStart =
            Time.time - cycleStartTime;

        Vector3 objectPos =
            currentObject.transform.position;

        // Accumulate path distance EVERY FRAME.
        // CSV writing can still remain at 30 Hz.
        if (hasLastObjectPosition) {
            actualPathLength +=
                Vector3.Distance(
                    lastObjectPosition,
                    objectPos);
        }

        lastObjectPosition = objectPos;
        hasLastObjectPosition = true;

        if (TrajectoryInterval > 0f) {
            if (tSinceCycleStart < nextTrajectoryLogTime)
                return;

            nextTrajectoryLogTime =
                tSinceCycleStart + TrajectoryInterval;
        }

        CacheBones();

        if (!bonesReady)
            return;

        float opennessDist;

        float openness =
            ComputePalmOpenness(out opennessDist);

        if (openness > maxOpenness)
            maxOpenness = openness;

        if (opennessDist > maxOpennessDistance)
            maxOpennessDistance = opennessDist;

        if (!recorded30 &&
            openness >= 30f &&
            wrist != null) {
            distanceAt30 =
                Vector3.Distance(
                    wrist.position,
                    currentObject.transform.position);

            recorded30 = true;
        }

        Vector3 basketPos =
            basketTransform != null
                ? basketTransform.position
                : Vector3.zero;

        Vector3 ring1 =
            GetRingPosition(0);

        Vector3 ring2 =
            GetRingPosition(1);

        Vector3 ring3 =
            GetRingPosition(2);

        int nextRequiredRing =
            pathController != null
                ? pathController.NextRequiredRingNumber
                : 0;

        int checkpointsCompleted =
            pathController != null
                ? pathController.CheckpointsCompleted
                : 0;

        int wrongCheckpointCount =
            pathController != null
                ? pathController.WrongCheckpointCount
                : 0;

        bool pathCompleted =
            pathController != null &&
            pathController.PathCompleted;

        bool basketObjectInside =
            placementZone != null &&
            placementZone.ObjectInside;

        float distanceToCurrentRing = 0f;

        if (pathController != null &&
            pathController.TryGetCurrentCheckpointPosition(
                out Vector3 currentRingPos)) {
            distanceToCurrentRing =
                Vector3.Distance(
                    objectPos,
                    currentRingPos);
        }

        float distanceToBasket =
            basketTransform != null
                ? Vector3.Distance(
                    objectPos,
                    basketTransform.position)
                : 0f;

        Vector3 wristPos =
            wrist != null ? wrist.position : Vector3.zero;

        Vector3 thumbPos =
            thumbTip != null ? thumbTip.position : Vector3.zero;

        Vector3 indexPos =
            indexTip != null ? indexTip.position : Vector3.zero;

        Vector3 middlePos =
            middleTip != null ? middleTip.position : Vector3.zero;

        Vector3 ringPos =
            ringTip != null ? ringTip.position : Vector3.zero;

        Vector3 pinkyPos =
            pinkyTip != null ? pinkyTip.position : Vector3.zero;

        List<string> row =
            BuildCommonFields("Trajectory");

        row.Add(Int(currentCycleIndex));
        row.Add(Csv(currentObjectName));
        row.Add(Float(tSinceCycleStart));
        row.Add(Bool01(isObjectGrabbed));
        row.Add(Float(currentContinuousGrabSeconds));
        row.Add(Float(openness, "F2"));
        row.Add(Float(opennessDist));

        AddVector3(row, objectPos);
        AddVector3(row, basketPos);

        AddVector3(row, ring1);
        AddVector3(row, ring2);
        AddVector3(row, ring3);

        row.Add(Int(nextRequiredRing));
        row.Add(Int(checkpointsCompleted));
        row.Add(Bool01(pathCompleted));
        row.Add(Int(wrongCheckpointCount));
        row.Add(Bool01(basketObjectInside));

        row.Add(Float(distanceToCurrentRing));
        row.Add(Float(distanceToBasket));
        row.Add(Float(actualPathLength));

        AddVector3(row, wristPos);
        AddVector3(row, thumbPos);
        AddVector3(row, indexPos);
        AddVector3(row, middlePos);
        AddVector3(row, ringPos);
        AddVector3(row, pinkyPos);

        AppendCsvLine(
            TrajectoryPath,
            row,
            TrajectoryHeaderColumns.Length);
    }

    public void EndCycle(
        string result,
        float totalTimeSeconds,
        float maxContinuousGrabSeconds) {
        if (config == null ||
            currentObject == null) {
            return;
        }

        Transform t =
            currentObject.transform;

        Vector3 basketPos =
            basketTransform != null
                ? basketTransform.position
                : Vector3.zero;

        Vector3 ring1 =
            GetRingPosition(0);

        Vector3 ring2 =
            GetRingPosition(1);

        Vector3 ring3 =
            GetRingPosition(2);

        float finalDistanceToBasket =
            basketTransform != null
                ? Vector3.Distance(
                    t.position,
                    basketTransform.position)
                : 0f;

        int wrongCheckpointCount =
            pathController != null
                ? pathController.WrongCheckpointCount
                : 0;

        int checkpointsCompleted =
            pathController != null
                ? pathController.CheckpointsCompleted
                : 0;

        bool pathCompleted =
            pathController != null &&
            pathController.PathCompleted;

        List<string> row =
            BuildCommonFields("Cycle");

        row.Add(Int(currentCycleIndex));
        row.Add(Csv(currentObjectName));
        row.Add(result);

        row.Add(Float(totalTimeSeconds));
        row.Add(Float(maxContinuousGrabSeconds));

        row.Add(Float(maxOpenness, "F2"));
        row.Add(Float(maxOpennessDistance));
        row.Add(Float(initialDistance));
        row.Add(Float(distanceAt30));

        row.Add(Float(actualPathLength));
        row.Add(Float(idealCenterlinePathLength));
        row.Add(Float(directFruitBasketDistance));
        row.Add(Float(finalDistanceToBasket));

        row.Add(Int(wrongCheckpointCount));
        row.Add(Int(checkpointsCompleted));
        row.Add(Bool01(pathCompleted));

        AddVector3(row, fruitStartPosition);
        AddVector3(row, basketPos);

        AddVector3(row, ring1);
        AddVector3(row, ring2);
        AddVector3(row, ring3);

        AddVector3(row, t.position);

        row.Add(Float(t.rotation.x, "F6"));
        row.Add(Float(t.rotation.y, "F6"));
        row.Add(Float(t.rotation.z, "F6"));
        row.Add(Float(t.rotation.w, "F6"));

        AddVector3(row, t.localScale);

        // SessionScore columns are empty for cycle rows.
        row.Add("");
        row.Add("");
        row.Add("");

        AppendCsvLine(
            ResultsPath,
            row,
            ResultsHeaderColumns.Length);

        FileLogger.Log(
            $"Stage4HandDataLogger: EndCycle " +
            $"cycle={currentCycleIndex}, result={result}");
    }

    public void EndSessionScore(
        int score,
        int totalCycles) {
        if (config == null)
            return;

        float successRate =
            totalCycles > 0
                ? ((float)score / totalCycles) * 100f
                : 0f;

        List<string> row =
            BuildCommonFields("SessionScore");

        // Fill everything except the final 3 score fields.
        while (row.Count <
               ResultsHeaderColumns.Length - 3) {
            row.Add("");
        }

        row.Add(Int(score));
        row.Add(Int(totalCycles));
        row.Add(Float(successRate, "F2"));

        AppendCsvLine(
            ResultsPath,
            row,
            ResultsHeaderColumns.Length);

        FileLogger.Log(
            $"Stage4HandDataLogger: Session score saved | " +
            $"Score={score}, Total={totalCycles}, " +
            $"SuccessRate={successRate:F1}%");
    }

    private float ComputeIdealCenterlinePathLength() {
        if (currentObject == null ||
            basketTransform == null ||
            pathController == null) {
            return 0f;
        }

        float distance = 0f;

        Vector3 previous =
            fruitStartPosition;

        for (int i = 0;
             i < pathController.CheckpointCount;
             i++) {
            Vector3 checkpoint =
                pathController.GetCheckpointPosition(i);

            distance +=
                Vector3.Distance(
                    previous,
                    checkpoint);

            previous = checkpoint;
        }

        distance +=
            Vector3.Distance(
                previous,
                basketTransform.position);

        return distance;
    }

    private Vector3 GetRingPosition(int index) {
        if (pathController == null ||
            index >= pathController.CheckpointCount) {
            return Vector3.zero;
        }

        return pathController.GetCheckpointPosition(index);
    }

    private void WriteSettingsRows() {
        List<string> resultsRow =
            BuildCommonFields("Settings");

        AppendCsvLine(
            ResultsPath,
            resultsRow,
            ResultsHeaderColumns.Length);

        List<string> trajectoryRow =
            BuildCommonFields("Settings");

        AppendCsvLine(
            TrajectoryPath,
            trajectoryRow,
            TrajectoryHeaderColumns.Length);
    }

    private List<string> BuildCommonFields(
        string rowType) {
        return new List<string>
        {
            rowType,
            Int(subjectId),
            sessionId,
            Double(UnixTime(), "F3"),
            "Stage4",

            config.handSelection.ToString(),
            config.endlessMode.ToString(),
            Int(config.trialsAmount),

            config.useTimeLimit.ToString(),
            Float(config.timeLimitSeconds, "F2"),

            config.targetDistance.ToString(),
            config.targetSize.ToString(),

            config.ringSize.ToString(),
            config.pathHeight.ToString()
        };
    }

    private void EnsureResultsHeader() {
        if (File.Exists(ResultsPath))
            return;

        File.AppendAllText(
            ResultsPath,
            string.Join(",", ResultsHeaderColumns) + "\n");
    }

    private void EnsureTrajectoryHeader() {
        if (File.Exists(TrajectoryPath))
            return;

        File.AppendAllText(
            TrajectoryPath,
            string.Join(",", TrajectoryHeaderColumns) + "\n");
    }

    private static void AppendCsvLine(
        string path,
        List<string> fields,
        int expectedColumnCount) {
        while (fields.Count < expectedColumnCount)
            fields.Add("");

        if (fields.Count != expectedColumnCount) {
            Debug.LogError(
                $"Stage4 CSV column mismatch. " +
                $"Expected={expectedColumnCount}, " +
                $"Actual={fields.Count}");
        }

        File.AppendAllText(
            path,
            string.Join(",", fields) + "\n");
    }

    private static void AddVector3(
        List<string> row,
        Vector3 value) {
        row.Add(Float(value.x, "F6"));
        row.Add(Float(value.y, "F6"));
        row.Add(Float(value.z, "F6"));
    }

    private void CacheBones() {
        if (bonesReady)
            return;

        if (handSkeleton == null ||
            handSkeleton.Bones == null ||
            handSkeleton.Bones.Count == 0) {
            return;
        }

        wrist =
            GetBone(OVRSkeleton.BoneId.Hand_WristRoot);

        thumbTip =
            GetBone(OVRSkeleton.BoneId.Hand_ThumbTip);

        indexTip =
            GetBone(OVRSkeleton.BoneId.Hand_IndexTip);

        middleTip =
            GetBone(OVRSkeleton.BoneId.Hand_MiddleTip);

        ringTip =
            GetBone(OVRSkeleton.BoneId.Hand_RingTip);

        pinkyTip =
            GetBone(OVRSkeleton.BoneId.Hand_PinkyTip);

        if (thumbTip == null) {
            thumbTip =
                GetBone(OVRSkeleton.BoneId.Hand_Thumb3);
        }

        if (indexTip == null) {
            indexTip =
                GetBone(OVRSkeleton.BoneId.Hand_Index3);
        }

        bonesReady =
            wrist != null &&
            thumbTip != null &&
            indexTip != null;
    }

    private Transform GetBone(
        OVRSkeleton.BoneId id) {
        if (handSkeleton == null ||
            handSkeleton.Bones == null) {
            return null;
        }

        foreach (var bone in handSkeleton.Bones) {
            if (bone.Id == id)
                return bone.Transform;
        }

        return null;
    }

    private float ComputePalmOpenness(
        out float distanceMeters) {
        distanceMeters = 0f;

        CacheBones();

        if (thumbTip == null ||
            indexTip == null) {
            return 0f;
        }

        distanceMeters =
            Vector3.Distance(
                indexTip.position,
                thumbTip.position);

        float openness01 =
            Mathf.Clamp01(
                (distanceMeters - closedThreshold) /
                (openThreshold - closedThreshold));

        return openness01 * 100f;
    }

    private static double UnixTime() {
        return
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds() / 1000.0;
    }

    private static string Csv(string value) {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") ||
            value.Contains("\"") ||
            value.Contains("\n")) {
            return "\"" +
                   value.Replace("\"", "\"\"") +
                   "\"";
        }

        return value;
    }

    private static string Bool01(bool value) {
        return value ? "1" : "0";
    }

    private static string Int(int value) {
        return value.ToString(
            CultureInfo.InvariantCulture);
    }

    private static string Float(
        float value,
        string format = "F4") {
        return value.ToString(
            format,
            CultureInfo.InvariantCulture);
    }

    private static string Double(
        double value,
        string format) {
        return value.ToString(
            format,
            CultureInfo.InvariantCulture);
    }
}