using System;
using System.IO;
using UnityEngine;

public class Stage1HandDataLogger : MonoBehaviour {
    [Header("Subject")]
    [SerializeField] private int subjectId = 1;

    [Header("CSV Files")]
    [SerializeField] private string resultsFileName = "stage1_results.csv";
    [SerializeField] private string trajectoryFileName = "stage1_hand_trajectory.csv";

    [Header("Trajectory Sampling")]
    [SerializeField] private float trajectoryLogHz = 30f;

    [Header("Palm Openness")]
    [SerializeField] private float closedThreshold = 0.02f;
    [SerializeField] private float openThreshold = 0.15f;

    private string ResultsPath => Path.Combine(Application.persistentDataPath, resultsFileName);
    private string TrajectoryPath => Path.Combine(Application.persistentDataPath, trajectoryFileName);

    private Stage1Config config;
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

    private float TrajectoryInterval => trajectoryLogHz <= 0f ? 0f : 1f / trajectoryLogHz;

    public void BeginSession(Stage1Config stageConfig, OVRSkeleton skeleton) {
        config = stageConfig;
        handSkeleton = skeleton;

        sessionId = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        bonesReady = false;
        CacheBones();

        EnsureResultsHeader();
        EnsureTrajectoryHeader();

        WriteSettingsRows();

        FileLogger.Log($"Stage1HandDataLogger: BeginSession sessionId={sessionId}");
    }

    public void BeginCycle(int cycleIndex, GameObject targetObject) {
        currentCycleIndex = cycleIndex;
        currentObject = targetObject;
        currentObjectName = targetObject != null ? targetObject.name : "null";

        cycleStartTime = Time.time;
        nextTrajectoryLogTime = 0f;

        recorded30 = false;
        distanceAt30 = 0f;

        float opennessDist;
        float openness = ComputePalmOpenness(out opennessDist);

        maxOpenness = openness;
        maxOpennessDistance = opennessDist;

        initialDistance = 0f;
        if (wrist != null && currentObject != null)
            initialDistance = Vector3.Distance(wrist.position, currentObject.transform.position);

        FileLogger.Log($"Stage1HandDataLogger: BeginCycle cycle={cycleIndex}, object={currentObjectName}");
    }

    public void SampleTrajectory(bool isTouchingObject, float currentContinuousTouchSeconds) {
        if (config == null || handSkeleton == null || currentObject == null)
            return;

        float tSinceCycleStart = Time.time - cycleStartTime;

        if (TrajectoryInterval > 0f) {
            if (tSinceCycleStart < nextTrajectoryLogTime)
                return;

            nextTrajectoryLogTime = tSinceCycleStart + TrajectoryInterval;
        }

        CacheBones();
        if (!bonesReady)
            return;

        float opennessDist;
        float openness = ComputePalmOpenness(out opennessDist);

        if (openness > maxOpenness)
            maxOpenness = openness;

        if (opennessDist > maxOpennessDistance)
            maxOpennessDistance = opennessDist;

        if (!recorded30 && openness >= 30f && wrist != null && currentObject != null) {
            distanceAt30 = Vector3.Distance(wrist.position, currentObject.transform.position);
            recorded30 = true;
        }

        Vector3 wristPos = wrist != null ? wrist.position : Vector3.zero;
        Vector3 thumbPos = thumbTip != null ? thumbTip.position : Vector3.zero;
        Vector3 indexPos = indexTip != null ? indexTip.position : Vector3.zero;
        Vector3 middlePos = middleTip != null ? middleTip.position : Vector3.zero;
        Vector3 ringPos = ringTip != null ? ringTip.position : Vector3.zero;
        Vector3 pinkyPos = pinkyTip != null ? pinkyTip.position : Vector3.zero;

        Vector3 objectPos = currentObject.transform.position;

        string row =
            "Trajectory," +
            $"{subjectId}," +
            $"{sessionId}," +
            $"{UnixTime():F3}," +
            "Stage1," +
            $"{config.handSelection}," +
            $"{config.endlessMode}," +
            $"{config.trialsAmount}," +
            $"{config.useTimeLimit}," +
            $"{config.timeLimitSeconds:F2}," +
            $"{config.targetDistance}," +
            $"{config.targetSize}," +
            $"{config.touchMode}," +
            $"{config.holdDurationSeconds:F2}," +
            $"{currentCycleIndex}," +
            $"{Csv(currentObjectName)}," +
            $"{tSinceCycleStart:F4}," +
            $"{Bool01(isTouchingObject)}," +
            $"{currentContinuousTouchSeconds:F4}," +
            $"{openness:F2}," +
            $"{opennessDist:F4}," +
            $"{objectPos.x:F6},{objectPos.y:F6},{objectPos.z:F6}," +
            $"{wristPos.x:F6},{wristPos.y:F6},{wristPos.z:F6}," +
            $"{thumbPos.x:F6},{thumbPos.y:F6},{thumbPos.z:F6}," +
            $"{indexPos.x:F6},{indexPos.y:F6},{indexPos.z:F6}," +
            $"{middlePos.x:F6},{middlePos.y:F6},{middlePos.z:F6}," +
            $"{ringPos.x:F6},{ringPos.y:F6},{ringPos.z:F6}," +
            $"{pinkyPos.x:F6},{pinkyPos.y:F6},{pinkyPos.z:F6}";

        File.AppendAllText(TrajectoryPath, row + "\n");
    }

    public void EndCycle(string result, float totalTimeSeconds, float maxContinuousTouchSeconds) {
        if (config == null || currentObject == null)
            return;

        Transform t = currentObject.transform;

        string row =
            "Cycle," +
            $"{subjectId}," +
            $"{sessionId}," +
            $"{UnixTime():F3}," +
            "Stage1," +
            $"{config.handSelection}," +
            $"{config.endlessMode}," +
            $"{config.trialsAmount}," +
            $"{config.useTimeLimit}," +
            $"{config.timeLimitSeconds:F2}," +
            $"{config.targetDistance}," +
            $"{config.targetSize}," +
            $"{config.touchMode}," +
            $"{config.holdDurationSeconds:F2}," +
            $"{currentCycleIndex}," +
            $"{Csv(currentObjectName)}," +
            $"{result}," +
            $"{totalTimeSeconds:F4}," +
            $"{maxContinuousTouchSeconds:F4}," +
            $"{maxOpenness:F2}," +
            $"{maxOpennessDistance:F4}," +
            $"{initialDistance:F4}," +
            $"{distanceAt30:F4}," +
            $"{t.position.x:F6},{t.position.y:F6},{t.position.z:F6}," +
            $"{t.rotation.x:F6},{t.rotation.y:F6},{t.rotation.z:F6},{t.rotation.w:F6}," +
            $"{t.localScale.x:F6},{t.localScale.y:F6},{t.localScale.z:F6}";

        File.AppendAllText(ResultsPath, row + "\n");

        FileLogger.Log($"Stage1HandDataLogger: EndCycle cycle={currentCycleIndex}, result={result}");
    }

    private void WriteSettingsRows() {
        string settingsResultsRow =
            "Settings," +
            $"{subjectId}," +
            $"{sessionId}," +
            $"{UnixTime():F3}," +
            "Stage1," +
            $"{config.handSelection}," +
            $"{config.endlessMode}," +
            $"{config.trialsAmount}," +
            $"{config.useTimeLimit}," +
            $"{config.timeLimitSeconds:F2}," +
            $"{config.targetDistance}," +
            $"{config.targetSize}," +
            $"{config.touchMode}," +
            $"{config.holdDurationSeconds:F2}," +
            ",,,,,,,,,,,,,,";

        File.AppendAllText(ResultsPath, settingsResultsRow + "\n");

        string settingsTrajectoryRow =
            "Settings," +
            $"{subjectId}," +
            $"{sessionId}," +
            $"{UnixTime():F3}," +
            "Stage1," +
            $"{config.handSelection}," +
            $"{config.endlessMode}," +
            $"{config.trialsAmount}," +
            $"{config.useTimeLimit}," +
            $"{config.timeLimitSeconds:F2}," +
            $"{config.targetDistance}," +
            $"{config.targetSize}," +
            $"{config.touchMode}," +
            $"{config.holdDurationSeconds:F2}," +
            ",,,,,,,,,,,,,,,,,,,,,,,,,,";

        File.AppendAllText(TrajectoryPath, settingsTrajectoryRow + "\n");
    }

    private void EnsureResultsHeader() {
        if (File.Exists(ResultsPath))
            return;

        File.AppendAllText(
            ResultsPath,
            "RowType,Subject,SessionID,UnixTime_s,Stage,HandSelection,EndlessMode,TrialsAmount,UseTimeLimit,TimeLimitSeconds,TargetDistance,TargetSize,TouchMode,HoldDurationSeconds," +
            "CycleIndex,ObjectName,CycleResult,TotalTime_s,MaxContinuousTouch_s,MaxOpenness_pct,MaxOpennessDist_m,InitialDistance_m,DistancePalmOpened_m," +
            "ObjectPos_x,ObjectPos_y,ObjectPos_z,ObjectRot_x,ObjectRot_y,ObjectRot_z,ObjectRot_w,ObjectScale_x,ObjectScale_y,ObjectScale_z\n"
        );
    }

    private void EnsureTrajectoryHeader() {
        if (File.Exists(TrajectoryPath))
            return;

        File.AppendAllText(
            TrajectoryPath,
            "RowType,Subject,SessionID,UnixTime_s,Stage,HandSelection,EndlessMode,TrialsAmount,UseTimeLimit,TimeLimitSeconds,TargetDistance,TargetSize,TouchMode,HoldDurationSeconds," +
            "CycleIndex,ObjectName,tSinceCycleStart_s,IsTouchingObject,CurrentContinuousTouch_s,Openness_pct,ThumbIndexDistance_m," +
            "ObjectPos_x,ObjectPos_y,ObjectPos_z," +
            "Wrist_x,Wrist_y,Wrist_z," +
            "ThumbTip_x,ThumbTip_y,ThumbTip_z," +
            "IndexTip_x,IndexTip_y,IndexTip_z," +
            "MiddleTip_x,MiddleTip_y,MiddleTip_z," +
            "RingTip_x,RingTip_y,RingTip_z," +
            "PinkyTip_x,PinkyTip_y,PinkyTip_z\n"
        );
    }

    private void CacheBones() {
        if (bonesReady)
            return;

        if (handSkeleton == null || handSkeleton.Bones == null || handSkeleton.Bones.Count == 0)
            return;

        wrist = GetBone(OVRSkeleton.BoneId.Hand_WristRoot);
        thumbTip = GetBone(OVRSkeleton.BoneId.Hand_ThumbTip);
        indexTip = GetBone(OVRSkeleton.BoneId.Hand_IndexTip);
        middleTip = GetBone(OVRSkeleton.BoneId.Hand_MiddleTip);
        ringTip = GetBone(OVRSkeleton.BoneId.Hand_RingTip);
        pinkyTip = GetBone(OVRSkeleton.BoneId.Hand_PinkyTip);

        if (thumbTip == null) thumbTip = GetBone(OVRSkeleton.BoneId.Hand_Thumb3);
        if (indexTip == null) indexTip = GetBone(OVRSkeleton.BoneId.Hand_Index3);

        bonesReady = wrist != null && thumbTip != null && indexTip != null;
    }

    private Transform GetBone(OVRSkeleton.BoneId id) {
        if (handSkeleton == null || handSkeleton.Bones == null)
            return null;

        foreach (var bone in handSkeleton.Bones) {
            if (bone.Id == id)
                return bone.Transform;
        }

        return null;
    }

    private float ComputePalmOpenness(out float distanceMeters) {
        distanceMeters = 0f;

        CacheBones();

        if (thumbTip == null || indexTip == null)
            return 0f;

        distanceMeters = Vector3.Distance(indexTip.position, thumbTip.position);

        float openness01 = Mathf.Clamp01((distanceMeters - closedThreshold) / (openThreshold - closedThreshold));
        return openness01 * 100f;
    }

    private static double UnixTime() {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private static string Csv(string value) {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    private static int Bool01(bool value) {
        return value ? 1 : 0;
    }
}