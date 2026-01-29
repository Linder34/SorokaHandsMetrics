using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using TMPro;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.SceneManagement;
using System;

public class OVRHandCountdownCycle : MonoBehaviour {
    [Header("Objects Sets (assign both)")]
    [SerializeField] private Transform objects1Parent; // drag "Objects"
    [SerializeField] private Transform objects2Parent; // drag "Objects2"
    private Transform objectsParent;

    [Header("Extra Objects")]
    [Tooltip("Optional table object to hide after all cycles.")]
    public GameObject table;

    [Header("Hand Tracking Settings")]
    [Tooltip("OVRSkeleton used to compute palm openness and finger positions.")]
    public OVRSkeleton handSkeleton;

    [Tooltip("Optional OVRHand (used to check tracking status).")]
    public OVRHand hand;

    [Tooltip("Average distance (meters) when the hand is fully closed.")]
    public float closedThreshold = 0.02f;

    [Tooltip("Average distance (meters) when the hand is fully open.")]
    public float openThreshold = 0.15f;

    [Header("Right Hand Interactor")]
    [Tooltip("Assign the TouchHandGrabInteractor component from your right hand.")]
    [SerializeField] private TouchHandGrabInteractor rightHandInteractor;

    [Header("Instruction Dialog")]
    [Tooltip("Assign the Dialog root GameObject (the one called 'Dialog').")]
    [SerializeField] private GameObject dialogRoot;

    [Tooltip("Assign the TextMeshProUGUI on Dialog/Dialog_Text/Title.")]
    [SerializeField] private TextMeshProUGUI dialogText;

    [Header("Menu Controller")]
    [Tooltip("Assign the object that has your MenuModeController (or whatever script shows main menu).")]
    [SerializeField] private MenuModeController menuController;

    [Header("Restart Settings")]
    [Tooltip("Optional: InteractableUnityEventWrapper from your BigRedButton so we can hook into OnSelectEntered.")]
    [SerializeField] private InteractableUnityEventWrapper restartButtonWrapper;

    [Header("Cycle Flow")]
    [Tooltip("Shown at the beginning of each cycle before the pickup prompt.")]
    [SerializeField] private string returnToStartMessage = "Move your hand to the starting position";
    [SerializeField] private float returnToStartDuration = 3f;

    [Tooltip("Show the 'Start Next Cycle' prompt this long after grab is detected.")]
    [SerializeField] private float showNextCyclePromptDelay = 1f;

    [Header("Unstick / Return-to-place")]
    [Tooltip("After Start Next Cycle is pressed, if the hand is still selecting (stuck), break the selection.")]
    [SerializeField] private bool forceUnselectIfStillGrabbedOnNext = true;

    [Tooltip("If still grabbed, we try to break select by toggling the interactor for this many frames.")]
    [SerializeField] private int forceUnselectToggleFrames = 1;

    [Tooltip("If true, pressing Start Next Cycle restores ALL objects to their original placement.")]
    [SerializeField] private bool restoreAllObjectsOnNextCyclePress = true;

    [Header("Audio")]
    [Tooltip("AudioSource used for all prompts/object names.")]
    public AudioSource audioPlayer;

    [Tooltip("Audio clips for object names (clip.name must match objectName).")]
    public List<AudioClip> objectClips;

    [Header("Instruction Audio (assign in Inspector)")]
    [SerializeField] private AudioClip moveHandToStartClip;
    [SerializeField] private AudioClip pressStartNextCycleClip;

    [Header("CSV Output")]
    [SerializeField] private string outputFileName = "results.csv";
    private string CsvPath => Path.Combine(Application.persistentDataPath, outputFileName);

    [Header("Trajectory Log Output (separate file)")]
    [SerializeField] private string trajectoryFileName = "hand_trajectory.csv";
    private string TrajectoryPath => Path.Combine(Application.persistentDataPath, trajectoryFileName);

    [Tooltip("Log hand trajectory at most this many times per second (reduces file size).")]
    [SerializeField] private float trajectoryLogHz = 30f;

    [Header("Subject")]
    [Tooltip("For now keep as 1; edit later per participant.")]
    [SerializeField] private int subjectId = 1;

    private float _trajectoryLogInterval => (trajectoryLogHz <= 0f) ? 0f : (1f / trajectoryLogHz);
    private float _nextTrajectoryLogTime = 0f;

    private StreamWriter _trajectoryWriter;

    // IMPORTANT FIX:
    // Buffer trajectory rows in memory and ONLY write them when the cycle is ACCEPTED
    // (i.e., after Start Next Cycle was pressed). If Restart/Reset happens, we discard the buffer.
    private readonly List<string> _pendingTrajectoryRows = new List<string>(4096);

    private bool _initialized = false;
    private bool _resultsSaved = false;

    private int currentCycle = 0;
    private List<CycleData> cycleDataList = new List<CycleData>();
    private GameObject[] targetObjects;

    private bool _startNextCycleRequested = false;
    private bool _resetCycleRequested = false;

    private string _experimentId = null;

    private class CycleData {
        public string objectName;
        public float timeToGrab;
        public float maxOpenness;           // %
        public float maxOpennessDistance;   // meters (distance between thumb/index tips at max openness)
        public float initialOpenness;
        public float initialDistance;
        public float distanceAt30;
    }

    // --------- cache original object poses PER PARENT ----------
    private struct ObjSnapshot {
        public Vector3 pos;
        public Quaternion rot;
        public bool active;
    }

    private readonly Dictionary<Transform, Dictionary<GameObject, ObjSnapshot>> _originalByParent =
        new Dictionary<Transform, Dictionary<GameObject, ObjSnapshot>>();

    private void CacheOriginalIfNeeded(Transform parent) {
        if (parent == null) return;
        if (_originalByParent.ContainsKey(parent)) return;

        var dict = new Dictionary<GameObject, ObjSnapshot>();
        foreach (Transform t in parent) {
            dict[t.gameObject] = new ObjSnapshot
            {
                pos = t.position,
                rot = t.rotation,
                active = t.gameObject.activeSelf
            };
        }
        _originalByParent[parent] = dict;
    }

    private void RestoreObjectsToOriginalPlacement(Transform parent) {
        if (parent == null) return;
        if (!_originalByParent.TryGetValue(parent, out var dict)) return;

        foreach (var kv in dict) {
            var go = kv.Key;
            if (go == null) continue;

            var s = kv.Value;
            go.transform.position = s.pos;
            go.transform.rotation = s.rot;
            go.SetActive(s.active);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
    }
    // -----------------------------------------------------------

    // --------- cached bones for fast access ----------
    private Transform _wrist;
    private Transform _thumbTip;
    private Transform _indexTip;
    private Transform _middleTip;
    private Transform _ringTip;
    private Transform _pinkyTip;

    private bool _boneCacheReady = false;

    private void EnsureBoneCache() {
        if (_boneCacheReady) return;
        if (handSkeleton == null || handSkeleton.Bones == null || handSkeleton.Bones.Count == 0) return;

        _wrist = GetBoneTransform(OVRSkeleton.BoneId.Hand_WristRoot);
        _thumbTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_ThumbTip);
        _indexTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_IndexTip);
        _middleTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_MiddleTip);
        _ringTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_RingTip);
        _pinkyTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_PinkyTip);

        // Fallbacks (some rigs use *_3)
        if (_thumbTip == null) _thumbTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_Thumb3);
        if (_indexTip == null) _indexTip = GetBoneTransform(OVRSkeleton.BoneId.Hand_Index3);

        // tips other than thumb/index are "nice to have", not required
        _boneCacheReady = (_wrist != null && _thumbTip != null && _indexTip != null);
    }

    private Transform GetBoneTransform(OVRSkeleton.BoneId id) {
        if (handSkeleton == null || handSkeleton.Bones == null) return null;
        foreach (var bone in handSkeleton.Bones) {
            if (bone.Id == id) return bone.Transform;
        }
        return null;
    }
    // ------------------------------------------------

    private void Start() {
        HideDialog();
        InitializeIfNeeded();
    }

    public void InitializeIfNeeded() {
        if (_initialized) return;

        if (handSkeleton == null) handSkeleton = GetComponent<OVRSkeleton>();
        if (hand == null) hand = GetComponent<OVRHand>();

        if (restartButtonWrapper != null)
            restartButtonWrapper.WhenSelect.AddListener(RestartScene);

        _initialized = true;
    }

    /// <summary>Hook this from your Start Next Cycle button.</summary>
    public void RequestStartNextCycle() {
        _startNextCycleRequested = true;
    }

    /// <summary>Hook this from your Reset Cycle button.</summary>
    public void RequestResetCycle() {
        _resetCycleRequested = true;
    }

    private void PlayClip(AudioClip clip) {
        if (clip == null || audioPlayer == null) return;
        audioPlayer.Stop();
        audioPlayer.clip = clip;
        audioPlayer.Play();
    }

    private void StopAudio() {
        if (audioPlayer == null) return;
        audioPlayer.Stop();
        audioPlayer.clip = null;
    }

    private IEnumerator ForceUnselectWithFramesIfNeeded() {
        if (rightHandInteractor == null) yield break;
        if (rightHandInteractor.State != InteractorState.Select) yield break;

        rightHandInteractor.enabled = false;
        for (int i = 0; i < Mathf.Max(1, forceUnselectToggleFrames); i++) {
            yield return null;
        }
        rightHandInteractor.enabled = true;
        yield return null;
    }

    private bool ConsumeResetRequest() {
        if (!_resetCycleRequested) return false;
        _resetCycleRequested = false;
        return true;
    }

    // Call this from buttons:
    // StartExperiment(1) -> uses Objects
    // StartExperiment(2) -> uses Objects2
    public void StartExperiment(int whichObjects) {
        if (whichObjects == 1) {
            objectsParent = objects1Parent;
            if (objects2Parent != null) objects2Parent.gameObject.SetActive(false);
        }
        else if (whichObjects == 2) {
            objectsParent = objects2Parent;
            if (objects1Parent != null) objects1Parent.gameObject.SetActive(false);
        }
        else {
            Debug.LogError("[OVRHandCountdownCycle] StartExperiment(which) must be 1 or 2.");
            return;
        }

        if (objectsParent == null) {
            Debug.LogError("[OVRHandCountdownCycle] Chosen objects parent is not assigned in inspector!");
            return;
        }

        if (rightHandInteractor == null) {
            Debug.LogError("[OVRHandCountdownCycle] RightHandInteractor is not assigned!");
            return;
        }

        if (dialogRoot == null || dialogText == null) {
            Debug.LogError("[OVRHandCountdownCycle] DialogRoot / DialogText not assigned!");
            return;
        }

        CacheOriginalIfNeeded(objectsParent);

        if (!objectsParent.gameObject.activeSelf)
            objectsParent.gameObject.SetActive(true);

        HideRealObjects();

        targetObjects = objectsParent
            .Cast<Transform>()
            .Where(t => t.name != "Plane" && t.name != "BottlePlane")
            .OrderBy(_ => UnityEngine.Random.value)
            .Select(t => t.gameObject)
            .ToArray();

        currentCycle = 0;
        cycleDataList.Clear();
        _resultsSaved = false;

        // Create a stable experiment ID for BOTH logs
        _experimentId = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Clear any pending buffered trajectory from a previous run
        _pendingTrajectoryRows.Clear();

        OpenTrajectoryLogIfNeeded();

        StartCoroutine(CycleCoroutine());
    }

    // ---------------- Dialog helpers ----------------
    private void ShowDialog(string message) {
        if (dialogRoot != null) dialogRoot.SetActive(true);
        if (dialogText != null) dialogText.text = message;
    }

    private void HideDialog() {
        if (dialogText != null) dialogText.text = "";
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }

    private IEnumerator ResetCurrentCycle(GameObject currentObject) {
        StopAudio();

        // DISCARD buffered trajectory for this attempt
        _pendingTrajectoryRows.Clear();

        if (rightHandInteractor != null && rightHandInteractor.State == InteractorState.Select) {
            yield return ForceUnselectWithFramesIfNeeded();
        }

        // Give grab system time to detach
        yield return null;
        yield return null;

        // Restore ALL objects as requested
        RestoreObjectsToOriginalPlacement(objectsParent);
        HideRealObjects();

        HideDialog();
        yield return new WaitForSeconds(0.1f);
    }

    // ---------------- Experiment loop ----------------
    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            var currentObject = targetObjects[currentCycle];

            // Allow multiple attempts for same cycle index (Reset Cycle)
            while (true) {
                _startNextCycleRequested = false;

                // start attempt with empty buffer
                _pendingTrajectoryRows.Clear();

                var cycleData = new CycleData { objectName = currentObject.name };

                // A) return-to-start prompt + audio
                ShowDialog(returnToStartMessage);
                PlayClip(moveHandToStartClip);

                float t0 = 0f;
                while (t0 < returnToStartDuration) {
                    if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }
                    t0 += Time.deltaTime;
                    yield return null;
                }
                HideDialog();

                // B) pickup prompt + object audio
                if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }

                ShowDialog($"Pick up the {currentObject.name}!");
                AudioClip objClip = GetClipForObject(currentObject.name);
                if (objClip != null) PlayClip(objClip);

                float t1 = 0f;
                while (t1 < 2.5f) {
                    if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }
                    t1 += Time.deltaTime;
                    yield return null;
                }
                HideDialog();

                EnsureBoneCache();

                // initial openness/distance
                float initialDistMeters;
                cycleData.initialOpenness = ComputePalmOpenness(out initialDistMeters);

                cycleData.initialDistance = (_wrist != null)
                    ? Vector3.Distance(_wrist.position, currentObject.transform.position)
                    : 0f;

                float startTime = Time.time;
                float maxOpenness = cycleData.initialOpenness;
                float maxOpennessDistance = initialDistMeters;
                bool recorded30 = false;

                // Trajectory sampling control for this cycle
                _nextTrajectoryLogTime = 0f;

                // C) wait until grab (track openness + trajectory until grab)
                while (rightHandInteractor.State != InteractorState.Select) {
                    if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }

                    float currentDistMeters;
                    float openness = ComputePalmOpenness(out currentDistMeters);

                    if (openness > maxOpenness) maxOpenness = openness;
                    if (currentDistMeters > maxOpennessDistance) maxOpennessDistance = currentDistMeters;

                    if (!recorded30 && openness >= 30f && _wrist != null) {
                        cycleData.distanceAt30 = Vector3.Distance(_wrist.position, currentObject.transform.position);
                        recorded30 = true;
                    }

                    // Buffer trajectory rows (not written yet!)
                    LogTrajectorySampleIfDue(
                        cycleIndex: currentCycle,
                        objectName: currentObject.name,
                        tSinceCycleStart: Time.time - startTime,
                        opennessPct: openness
                    );

                    yield return null;
                }

                // Grab detected -> finalize cycle metrics
                cycleData.timeToGrab = Time.time - startTime;
                cycleData.maxOpenness = maxOpenness;
                cycleData.maxOpennessDistance = maxOpennessDistance;

                // D) delay after grab
                float t2 = 0f;
                while (t2 < showNextCyclePromptDelay) {
                    if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }
                    t2 += Time.deltaTime;
                    yield return null;
                }

                // E) show next cycle prompt + audio
                _startNextCycleRequested = false;
                ShowDialog("Press “Start Next Cycle” when you're ready.");
                PlayClip(pressStartNextCycleClip);

                // wait for either next OR reset
                while (!_startNextCycleRequested) {
                    if (ConsumeResetRequest()) { yield return ResetCurrentCycle(currentObject); goto RestartAttempt; }
                    yield return null;
                }

                HideDialog();

                // Cycle was ACCEPTED -> write buffered trajectory rows to file
                FlushPendingTrajectoryToFile();
                _pendingTrajectoryRows.Clear();

                // F) HARD RESET ON NEXT CYCLE PRESS
                if (forceUnselectIfStillGrabbedOnNext && rightHandInteractor.State == InteractorState.Select) {
                    yield return ForceUnselectWithFramesIfNeeded();
                }

                // Give grab system time to detach before restoring transforms
                yield return null;
                yield return null;

                if (restoreAllObjectsOnNextCyclePress) {
                    RestoreObjectsToOriginalPlacement(objectsParent);
                    HideRealObjects();
                }

                // Commit cycle data and advance
                cycleDataList.Add(cycleData);
                currentCycle++;

                yield return new WaitForSeconds(0.2f);
                break;

            RestartAttempt:
                continue;
            }
        }

        ShowResultsPopup();

        RestoreObjectsToOriginalPlacement(objectsParent);

        if (objectsParent != null) objectsParent.gameObject.SetActive(false);
        if (table != null) table.SetActive(false);

        HideDialog();

        CloseTrajectoryLog();

        if (menuController != null) {
            menuController.EnterMainMenu();
        }
        else {
            Debug.LogWarning("[OVRHandCountdownCycle] MenuController not assigned. Can't show main menu.");
        }
    }

    private void ShowResultsPopup() {
        if (dialogRoot == null || dialogText == null) {
            Debug.LogWarning("[OVRHandCountdownCycle] DialogRoot/DialogText missing; can't show results.");
            return;
        }

        List<string> lines = new List<string>();
        foreach (CycleData data in cycleDataList) {
            string line =
                $"{data.objectName}:\n" +
                $"Total Time(s): {data.timeToGrab:F2}\n" +
                $"Max Openness(%): {data.maxOpenness:F1}\n" +
                $"Max Open Dist(m): {data.maxOpennessDistance:F3}\n" +
                $"Initial Distance(m): {data.initialDistance:F2}\n" +
                $"Distance Palm Opened(m): {data.distanceAt30:F2}";
            lines.Add(line);
        }

        string finalText = string.Join("\n\n", lines);
        dialogRoot.SetActive(true);
        dialogText.text = finalText;

        WriteCsvAll();
        _resultsSaved = true;
    }

    // ---------------- Object filtering ----------------
    public void HideRealObjects() {
        if (objectsParent == null) {
            Debug.LogWarning("[OVRHandCountdownCycle] objectsParent is not assigned!");
            return;
        }

        foreach (Transform child in objectsParent) {
            if (child.name.Contains("Real")) {
                MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers) {
                    if (r.name == "PositionGlow") continue;
                    r.enabled = false;
                }
            }
        }
    }

    // ---------------- Audio ----------------
    private AudioClip GetClipForObject(string objectName) {
        if (objectClips == null) return null;

        foreach (var clip in objectClips) {
            if (clip != null && clip.name == objectName)
                return clip;
        }
        return null;
    }

    // ---------------- Palm openness ----------------
    private float ComputePalmOpenness(out float distanceMeters) {
        distanceMeters = 0f;

        if (handSkeleton == null || handSkeleton.Bones == null || handSkeleton.Bones.Count == 0)
            return 0f;

        EnsureBoneCache();

        if (_indexTip == null || _thumbTip == null)
            return 0f;

        float distance = Vector3.Distance(_indexTip.position, _thumbTip.position);
        distanceMeters = distance;

        float minDistance = closedThreshold;
        float maxDistance = openThreshold;

        float openness01 = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        return openness01 * 100f;
    }

    // ---------------- Trajectory logging (BUFFERED) ----------------
    private void OpenTrajectoryLogIfNeeded() {
        try {
            bool isNew = !File.Exists(TrajectoryPath);
            _trajectoryWriter = new StreamWriter(TrajectoryPath, append: true);

            if (isNew) {
                _trajectoryWriter.WriteLine(
                    "Subject,ExperimentID,UnixTime_s,CycleIndex,ObjectName,tSinceCycleStart_s,Openness_pct," +
                    "Wrist_x,Wrist_y,Wrist_z," +
                    "ThumbTip_x,ThumbTip_y,ThumbTip_z," +
                    "IndexTip_x,IndexTip_y,IndexTip_z," +
                    "MiddleTip_x,MiddleTip_y,MiddleTip_z," +
                    "RingTip_x,RingTip_y,RingTip_z," +
                    "PinkyTip_x,PinkyTip_y,PinkyTip_z"
                );
                _trajectoryWriter.Flush();
            }

            Debug.Log($"[Trajectory] Logging to: {TrajectoryPath}");
        }
        catch (Exception e) {
            Debug.LogError($"[Trajectory] Failed to open trajectory file: {e}");
            _trajectoryWriter = null;
        }
    }

    private void CloseTrajectoryLog() {
        if (_trajectoryWriter == null) return;
        try {
            _trajectoryWriter.Flush();
            _trajectoryWriter.Close();
        }
        catch { }
        finally {
            _trajectoryWriter = null;
        }
    }

    private void LogTrajectorySampleIfDue(int cycleIndex, string objectName, float tSinceCycleStart, float opennessPct) {
        if (_trajectoryWriter == null) return;

        if (_trajectoryLogInterval > 0f) {
            if (tSinceCycleStart < _nextTrajectoryLogTime) return;
            _nextTrajectoryLogTime = tSinceCycleStart + _trajectoryLogInterval;
        }

        EnsureBoneCache();
        if (!_boneCacheReady) return;

        double unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        Vector3 wrist = _wrist != null ? _wrist.position : Vector3.zero;
        Vector3 thumb = _thumbTip != null ? _thumbTip.position : Vector3.zero;
        Vector3 index = _indexTip != null ? _indexTip.position : Vector3.zero;
        Vector3 middle = _middleTip != null ? _middleTip.position : Vector3.zero;
        Vector3 ring = _ringTip != null ? _ringTip.position : Vector3.zero;
        Vector3 pinky = _pinkyTip != null ? _pinkyTip.position : Vector3.zero;

        // Buffer row in memory; do NOT write to disk yet.
        string row =
            $"{subjectId}," +
            $"{_experimentId}," +
            $"{unixTime:F3}," +
            $"{cycleIndex}," +
            $"{objectName}," +
            $"{tSinceCycleStart:F4}," +
            $"{opennessPct:F2}," +
            $"{wrist.x:F6},{wrist.y:F6},{wrist.z:F6}," +
            $"{thumb.x:F6},{thumb.y:F6},{thumb.z:F6}," +
            $"{index.x:F6},{index.y:F6},{index.z:F6}," +
            $"{middle.x:F6},{middle.y:F6},{middle.z:F6}," +
            $"{ring.x:F6},{ring.y:F6},{ring.z:F6}," +
            $"{pinky.x:F6},{pinky.y:F6},{pinky.z:F6}";

        _pendingTrajectoryRows.Add(row);
    }

    // Only called when the cycle is ACCEPTED (Start Next Cycle pressed).
    private void FlushPendingTrajectoryToFile() {
        if (_trajectoryWriter == null) return;
        if (_pendingTrajectoryRows.Count == 0) return;

        for (int i = 0; i < _pendingTrajectoryRows.Count; i++) {
            _trajectoryWriter.WriteLine(_pendingTrajectoryRows[i]);
        }
        _trajectoryWriter.Flush();
    }

    // ---------------- Restart / CSV ----------------
    private void RestartScene() {
        // IMPORTANT FIX:
        // If user hits Restart, we DISCARD any buffered trajectory rows (uncommitted attempt)
        _pendingTrajectoryRows.Clear();

        if (!_resultsSaved) {
            for (int i = currentCycle; i < targetObjects.Length; i++) {
                cycleDataList.Add(new CycleData
                {
                    objectName = targetObjects[i].name,
                    timeToGrab = 0f,
                    maxOpenness = 0f,
                    maxOpennessDistance = 0f,
                    initialOpenness = 0f,
                    initialDistance = 0f,
                    distanceAt30 = 0f
                });
            }

            WriteCsvAll();
            _resultsSaved = true;
        }

        CloseTrajectoryLog();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void WriteCsvAll() {
        string path = CsvPath;
        bool isNew = !File.Exists(path);

        using (var sw = new StreamWriter(path, append: true)) {
            if (isNew) {
                sw.WriteLine("ExperimentID,ObjectName,TotalTime_s,MaxOpenness_pct,MaxOpennessDist_m,InitialDistance_m,DistancePalmOpened_m");
            }

            string experimentId = string.IsNullOrEmpty(_experimentId)
                ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
                : _experimentId;

            foreach (var d in cycleDataList) {
                sw.WriteLine(
                    $"{experimentId}," +
                    $"{d.objectName}," +
                    $"{d.timeToGrab:F2}," +
                    $"{d.maxOpenness:F1}," +
                    $"{d.maxOpennessDistance:F3}," +
                    $"{d.initialDistance:F2}," +
                    $"{d.distanceAt30:F2}"
                );
            }
        }

        Debug.Log($"[CSV] Wrote ({cycleDataList.Count} rows) to {path}");
    }
}
