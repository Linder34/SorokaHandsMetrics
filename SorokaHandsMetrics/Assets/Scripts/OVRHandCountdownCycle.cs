using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using TMPro;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.SceneManagement;

public class OVRHandCountdownCycle : MonoBehaviour {
    


    [Header("Objects Sets (assign both)")]
    [SerializeField] private Transform objects1Parent; // drag "Objects"
    [SerializeField] private Transform objects2Parent; // drag "Objects2"
    private Transform objectsParent;

    [Header("Extra Objects")]
    [Tooltip("Optional table object to hide after all cycles.")]
    public GameObject table;

    [Header("Hand Tracking Settings")]
    [Tooltip("OVRSkeleton used to compute palm openness.")]
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

    [Header("Audio")]
    public AudioSource audioPlayer;
    public List<AudioClip> objectClips;

    [Header("CSV Output")]
    [SerializeField] private string outputFileName = "results.csv";
    private string CsvPath => Path.Combine(Application.persistentDataPath, outputFileName);

    private bool _initialized = false;
    private bool _resultsSaved = false;

    private int currentCycle = 0;
    private List<CycleData> cycleDataList = new List<CycleData>();
    private GameObject[] targetObjects;

    private class CycleData {
        public string objectName;
        public float timeToGrab;
        public float maxOpenness;           // %
        public float maxOpennessDistance;   // meters (thumb–index distance at max openness)
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
            }
        }
    }
    // -----------------------------------------------------------

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

    // Call this from buttons:
    // StartExperiment(1) -> uses Objects
    // StartExperiment(2) -> uses Objects2
    public void StartExperiment(int whichObjects) {
        // choose objects parent
        if (whichObjects == 1) {
            objectsParent = objects1Parent;
            objects2Parent.gameObject.SetActive(false);
        }
        else if (whichObjects == 2) { 
            objectsParent = objects2Parent;
            objects1Parent.gameObject.SetActive(false);
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

        // cache originals for this set (once)
        CacheOriginalIfNeeded(objectsParent);

        if (!objectsParent.gameObject.activeSelf)
            objectsParent.gameObject.SetActive(true);

        HideRealObjects();

        targetObjects = objectsParent
            .Cast<Transform>()
            .Where(t => t.name != "Plane" && t.name != "BottlePlane")
            .OrderBy(_ => Random.value)
            .Select(t => t.gameObject)
            .ToArray();

        currentCycle = 0;
        cycleDataList.Clear();
        _resultsSaved = false;

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

    // ---------------- Experiment loop ----------------
    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            var currentObject = targetObjects[currentCycle];
            var cycleData = new CycleData { objectName = currentObject.name };

            ShowDialog($"Pick up the {currentObject.name}!");

            AudioClip clip = GetClipForObject(currentObject.name);
            if (clip != null && audioPlayer != null) {
                audioPlayer.clip = clip;
                audioPlayer.Play();
            }

            yield return new WaitForSeconds(2.5f);
            HideDialog();

            float initialDistMeters;
            cycleData.initialOpenness = ComputePalmOpenness(out initialDistMeters);

            var wrist = GetWristTransform();
            cycleData.initialDistance = wrist != null
                ? Vector3.Distance(wrist.position, currentObject.transform.position)
                : 0f;

            float startTime = Time.time;
            float maxOpenness = cycleData.initialOpenness;
            float maxOpennessDistance = initialDistMeters;
            bool recorded30 = false;

            while (rightHandInteractor.State != InteractorState.Select) {
                float currentDistMeters;
                float openness = ComputePalmOpenness(out currentDistMeters);

                if (openness > maxOpenness) maxOpenness = openness;
                if (currentDistMeters > maxOpennessDistance) maxOpennessDistance = currentDistMeters;

                if (!recorded30 && openness >= 30f && wrist != null) {
                    cycleData.distanceAt30 = Vector3.Distance(wrist.position, currentObject.transform.position);
                    recorded30 = true;
                }

                yield return null;
            }

            cycleData.timeToGrab = Time.time - startTime;
            cycleData.maxOpenness = maxOpenness;
            cycleData.maxOpennessDistance = maxOpennessDistance;

            float timer = 0f;
            while (rightHandInteractor.State == InteractorState.Select && timer < 2f) {
                timer += Time.deltaTime;
                yield return null;
            }

            cycleDataList.Add(cycleData);
            currentCycle++;

            yield return new WaitForSeconds(1f);
        }

        // keep your behavior: show results + write csv
        ShowResultsPopup();

        // then reset ONLY the set we used this run
        RestoreObjectsToOriginalPlacement(objectsParent);

        // hide for next run
        if (objectsParent != null) objectsParent.gameObject.SetActive(false);
        if (table != null) table.SetActive(false);

        HideDialog();

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
                foreach (var r in renderers) r.enabled = false;
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

        Transform indexTip = null;
        Transform thumbTip = null;

        foreach (var bone in handSkeleton.Bones) {
            if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip || bone.Id == OVRSkeleton.BoneId.Hand_Index3)
                indexTip = bone.Transform;
            else if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip || bone.Id == OVRSkeleton.BoneId.Hand_Thumb3)
                thumbTip = bone.Transform;
        }

        if (indexTip == null || thumbTip == null)
            return 0f;

        float distance = Vector3.Distance(indexTip.position, thumbTip.position);
        distanceMeters = distance;

        float minDistance = closedThreshold;
        float maxDistance = openThreshold;

        float openness01 = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        return openness01 * 100f;
    }

    private Transform GetWristTransform() {
        if (handSkeleton == null || handSkeleton.Bones == null)
            return null;

        foreach (var bone in handSkeleton.Bones) {
            if (bone.Id == OVRSkeleton.BoneId.Hand_WristRoot)
                return bone.Transform;
        }

        return null;
    }

    // ---------------- Restart / CSV ----------------
    private void RestartScene() {
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

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void WriteCsvAll() {
        string path = CsvPath;
        bool isNew = !File.Exists(path);

        using (var sw = new StreamWriter(path, append: true)) {
            if (isNew) {
                sw.WriteLine("ExperimentID,ObjectName,TotalTime_s,MaxOpenness_pct,MaxOpennessDist_m,InitialDistance_m,DistancePalmOpened_m");
            }

            string experimentId = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
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
