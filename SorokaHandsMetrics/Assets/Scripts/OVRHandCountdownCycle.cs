using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Oculus.Interaction.HandGrab; // For TouchHandGrabInteractor
using UnityEngine.UI;               // For Image
using Oculus.Interaction;           // For InteractorState
using UnityEngine.SceneManagement;
using System.Linq;
using System.IO;     // for File & StreamWriter

public class OVRHandCountdownCycle : MonoBehaviour {
    [Header("Target Objects Container")]
    [Tooltip("Assign the parent GameObject that contains all target objects as children.")]
    public Transform objectsParent;

    [Header("Extra Objects")]
    [Tooltip("Optional table object to hide after all cycles.")]
    public GameObject table;

    [Header("Hand Tracking Settings")]
    [Tooltip("OVRSkeleton used to compute palm openness.")]
    public OVRSkeleton handSkeleton;
    [Tooltip("Optional OVRHand (used to check tracking status).")]
    public OVRHand hand;
    [Tooltip("Average distance (meters) when the hand is fully closed.")]
    public float closedThreshold = 0.02f; // Adjusted for index/thumb calculation
    [Tooltip("Average distance (meters) when the hand is fully open.")]
    public float openThreshold = 0.15f;

    [Header("Right Hand Interactor")]
    [Tooltip("Assign the TouchHandGrabInteractor component from your right hand.")]
    [SerializeField] private TouchHandGrabInteractor rightHandInteractor;

    [Header("Results Popup (and Countdown)")]
    [Tooltip("Assign the TextMeshProUGUI element (already in your scene) that will display the countdown messages and final results. Its parent should have an Image component for the background.")]
    public TextMeshProUGUI resultsPopupText;

    [Header("Restart Settings")]
    [Tooltip("Drag here the InteractableUnityEventWrapper from your BigRedButton/Button child so we can hook into OnSelectEntered.")]
    [SerializeField] private InteractableUnityEventWrapper restartButtonWrapper;

    [Header("Audio")]
    public AudioSource audioPlayer;            // A single AudioSource somewhere in the scene
    public List<AudioClip> objectClips;      // or AudioClip[] if you prefer

    [SerializeField] private string outputFileName = "results.csv";
    private string CsvPath => Path.Combine(Application.persistentDataPath, outputFileName);

    private bool _resultsSaved = false;

    // Store the original background size.
    private Vector2 originalBgSize;

    // Cycle counter and storage.
    private int currentCycle = 0;
    private List<CycleData> cycleDataList = new List<CycleData>();

    // Runtime array of target objects.
    private GameObject[] targetObjects;

    // Structure to hold per-cycle metrics.
    private class CycleData {
        public string objectName;
        public float timeToGrab;
        public float maxOpenness;           // %
        public float maxOpennessDistance;   // meters (thumb–index distance at max openness)
        public float initialOpenness;
        public float initialDistance;
        public float distanceAt30;
    }

    private void Start() {
        if (objectsParent == null) {
            Debug.LogError("Please assign the Objects parent in the Inspector!");
            return;
        }

        HideRealObjects();

        targetObjects = objectsParent
            .Cast<Transform>()
            .Where(t => t.name != "Plane" && t.name != "BottlePlane")
            .OrderBy(_ => Random.value)        // Random.value is [0..1)
            .Select(t => t.gameObject)
            .ToArray();

        if (handSkeleton == null) handSkeleton = GetComponent<OVRSkeleton>();
        if (hand == null) hand = GetComponent<OVRHand>();

        if (resultsPopupText == null) {
            Debug.LogWarning("Results Popup Text is not assigned in the Inspector!");
        }
        else {
            RectTransform textRect = resultsPopupText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;

            Image bg = resultsPopupText.GetComponentInParent<Image>();
            if (bg != null) {
                RectTransform bgRect = bg.GetComponent<RectTransform>();
                originalBgSize = bgRect.sizeDelta;
            }
        }

        if (restartButtonWrapper != null) {
            restartButtonWrapper.WhenSelect.AddListener(RestartScene);
        }
        else {
            Debug.LogWarning("RestartButtonWrapper not assigned; scene restart won't work.");
        }

        StartCoroutine(CycleCoroutine());
    }

    private AudioClip GetClipForObject(string objectName) {
        // Example: objectName = "Bottle"
        // Clip name should be "Bottle"
        foreach (var clip in objectClips) {
            if (clip != null && clip.name == objectName)
                return clip;
        }
        return null;
    }

    public void HideRealObjects() {
        if (objectsParent == null) {
            Debug.LogWarning("SetupSceneController: objectsParent is not assigned!");
            return;
        }

        foreach (Transform child in objectsParent) {
            if (child.name.Contains("Real")) {
                // Disable ALL MeshRenderers under this object
                MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers) {
                    r.enabled = false;
                }
            }
        }
    }

    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            var currentObject = targetObjects[currentCycle];
            var cycleData = new CycleData { objectName = currentObject.name };

            // Countdown
            float originalFontSize = resultsPopupText.fontSize;
            resultsPopupText.fontSize = originalFontSize * 3f;
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0.7f));

            resultsPopupText.text = $"Pick up the {currentObject.name}!";

            // Get audio clip that matches the object name
            AudioClip clip = GetClipForObject(currentObject.name);

            if (clip != null && audioPlayer != null) {
                audioPlayer.clip = clip;
                audioPlayer.Play();
            }

            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(2.5f);

            resultsPopupText.text = "";
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0f));
            resultsPopupText.fontSize = originalFontSize;
            RestorePopupBackgroundSize();

            // Initial metrics
            float initialDistMeters;
            cycleData.initialOpenness = ComputePalmOpenness(out initialDistMeters);

            var wrist = GetWristTransform();
            cycleData.initialDistance = wrist != null
                ? Vector3.Distance(wrist.position, currentObject.transform.position)
                : 0f;

            float startTime = Time.time;
            float maxOpenness = cycleData.initialOpenness;
            float maxOpennessDistance = initialDistMeters; // start with initial distance
            bool recorded30 = false;

            while (rightHandInteractor.State != InteractorState.Select) {
                float currentDistMeters;
                float openness = ComputePalmOpenness(out currentDistMeters);

                // track max openness % and distance
                if (openness > maxOpenness) {
                    maxOpenness = openness;
                }
                if (currentDistMeters > maxOpennessDistance) {
                    maxOpennessDistance = currentDistMeters;
                }

                if (!recorded30 && openness >= 30f && wrist != null) {
                    cycleData.distanceAt30 = Vector3.Distance(wrist.position, currentObject.transform.position);
                    recorded30 = true;
                }
                yield return null;
            }

            cycleData.timeToGrab = Time.time - startTime;
            cycleData.maxOpenness = maxOpenness;
            cycleData.maxOpennessDistance = maxOpennessDistance;

            Debug.Log(
                $"[{currentObject.name}] Distance start: {cycleData.initialDistance:F2}m, " +
                $"at30%: {cycleData.distanceAt30:F2}m, time: {cycleData.timeToGrab:F2}s, " +
                $"maxOpen: {cycleData.maxOpenness:F1}%, " +
                $"maxOpenDist: {cycleData.maxOpennessDistance:F3}m");

            float timer = 0f;
            while (rightHandInteractor.State == InteractorState.Select && timer < 2f) {
                timer += Time.deltaTime;
                yield return null;
            }

            cycleDataList.Add(cycleData);
            currentCycle++;
            yield return new WaitForSeconds(1f);
        }

        foreach (var obj in targetObjects) if (obj != null) obj.SetActive(false);
        if (table != null) table.SetActive(false);

        SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0.7f));
        ShowResultsPopup();
    }

    private void UpdateCountdownBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(7f, 3f);
            Vector2 textCenter = resultsPopupText.textBounds.center;
            bgRect.anchoredPosition = new Vector2(bgRect.anchoredPosition.x, textCenter.y + 2f);
        }
    }

    private void RestorePopupBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = originalBgSize;
        }
    }

    private void SetPopupBackgroundColor(Color color) {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            bg.color = color;
        }
    }

    /// <summary>
    /// Computes palm openness (% 0-100) AND returns the raw thumb-index distance in meters.
    /// </summary>
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

        float minDistance = 0.02f;
        float maxDistance = 0.15f;
        float openness01 = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        float palmOpennessPercent = openness01 * 100f;

        Debug.Log($"Palm Openness (Index/Thumb): {palmOpennessPercent:F2}% | Dist: {distanceMeters:F3}m");
        return palmOpennessPercent;
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

    private void UpdateResultsPopupBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(12f, 10f);
        }
    }

    private void ShowResultsPopup() {
        if (resultsPopupText == null) {
            Debug.LogWarning("Results Popup Text is not assigned in the Inspector!");
            return;
        }
        List<string> lines = new List<string>();
        foreach (CycleData data in cycleDataList) {
            string line =
                $"<color=#FF69B4>{data.objectName}</color>:\n" +
                $"Total Time(s): <color=orange>{data.timeToGrab:F2}</color>, " +
                $"Max Openness(%): <color=orange>{data.maxOpenness:F1}</color>, " +
                $"Max Open Dist(m): <color=orange>{data.maxOpennessDistance:F3}</color>, " +
                $"Initial Distance(m): <color=orange>{data.initialDistance:F2}</color>, " +
                $"Distance Palm Opened(m): <color=orange>{data.distanceAt30:F2}</color>";
            lines.Add(line);
        }
        string finalText = string.Join("\n\n", lines);
        resultsPopupText.text = finalText;
        UpdateResultsPopupBackgroundSize();

        WriteCsvAll();
        _resultsSaved = true;
    }

    private void RestartScene() {
        if (!_resultsSaved) {
            // pad out zero-entries for any untested objects
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
        foreach (var obj in targetObjects) if (obj != null && !obj.activeSelf) obj.SetActive(true);
    }

    private void AppendCsvLine(CycleData d) {
        bool isNew = !File.Exists(CsvPath);

        using (var sw = new StreamWriter(CsvPath, append: true)) {
            if (isNew) {
                sw.WriteLine(
                    "ExperimentID,ObjectName,TotalTime_s,MaxOpenness_pct,MaxOpennessDist_m,InitialDistance_m,DistancePalmOpened_m"
                );
            }
            string experimentId = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            sw.WriteLine(
                $"{experimentId}," +
                $"{d.objectName}," +
                $"{d.timeToGrab:F2}," +
                $"{d.maxOpenness:F1}," +
                $"{d.maxOpennessDistance:F3}," +
                $"{d.initialDistance:F2}," +
                $"{d.distanceAt30:F2}");
        }

        Debug.Log($"[CSV] Appended {d.objectName} to {CsvPath}");
    }

    private void WriteCsvAll() {
        string path = CsvPath;
        bool isNew = !File.Exists(path);

        using (var sw = new StreamWriter(path, append: true)) {
            if (isNew) {
                sw.WriteLine(
                    "ExperimentID,ObjectName,TotalTime_s,MaxOpenness_pct,MaxOpennessDist_m,InitialDistance_m,DistancePalmOpened_m"
                );
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
                    $"{d.distanceAt30:F2}");
            }
        }

        Debug.Log($"[CSV] Wrote full test ({cycleDataList.Count} rows) to {path}");
    }
}
