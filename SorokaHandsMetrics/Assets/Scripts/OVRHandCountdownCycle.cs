using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Oculus.Interaction.HandGrab; // For TouchHandGrabInteractor
using UnityEngine.UI;               // For Image
using Oculus.Interaction;           // For InteractorState
using UnityEngine.SceneManagement;
using System.Linq;


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
        public float maxOpenness;
        public float initialOpenness;
        public float initialDistance;
        public float distanceAt30;
    }

    private void Start() {
        // ————— your original initialization —————
        if (objectsParent == null) {
            Debug.LogError("Please assign the Objects parent in the Inspector!");
            return;
        }
        //var tennisBall = objectsParent.Find("Tennis Ball")?.gameObject;
        //var plasticCup = objectsParent.Find("Plastic Cup")?.gameObject;
        //var capsule = objectsParent.Find("Capsule")?.gameObject;
        //var coffeeMug = objectsParent.Find("Coffee Mug")?.gameObject;
        //var cube = objectsParent.Find("Cube")?.gameObject;
        //targetObjects = new GameObject[] { tennisBall, plasticCup, capsule, coffeeMug, cube };
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

        // ————— subscribe restart callback —————
        if (restartButtonWrapper != null) {
            restartButtonWrapper.WhenSelect.AddListener(RestartScene);
            //restartButtonWrapper.SetActive(false);
        }
        else {
            Debug.LogWarning("RestartButtonWrapper not assigned; scene restart won't work.");
        }

        StartCoroutine(CycleCoroutine());
    }


    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            var currentObject = targetObjects[currentCycle];
            var cycleData = new CycleData { objectName = currentObject.name };

            // Countdown
            float originalFontSize = resultsPopupText.fontSize;
            resultsPopupText.fontSize = originalFontSize * 3f;
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0.7f));

            for (int i = 3; i > 0; i--) {
                resultsPopupText.text = i.ToString();
                UpdateCountdownBackgroundSize();
                yield return new WaitForSeconds(1f);
            }
            resultsPopupText.text = $"Pick up the {currentObject.name}!";
            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(2.5f);

            resultsPopupText.text = "";
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0f));
            resultsPopupText.fontSize = originalFontSize;
            RestorePopupBackgroundSize();

            // Initial metrics
            cycleData.initialOpenness = ComputePalmOpenness();
            var wrist = GetWristTransform();
            cycleData.initialDistance = wrist != null ? Vector3.Distance(wrist.position, currentObject.transform.position) : 0f;

            float startTime = Time.time;
            float maxOpenness = cycleData.initialOpenness;
            bool recorded30 = false;

            while (rightHandInteractor.State != InteractorState.Select) {
                float openness = ComputePalmOpenness();
                maxOpenness = Mathf.Max(maxOpenness, openness);
                if (!recorded30 && openness >= 30f && wrist != null) {
                    cycleData.distanceAt30 = Vector3.Distance(wrist.position, currentObject.transform.position);
                    recorded30 = true;
                }
                yield return null;
            }

            cycleData.timeToGrab = Time.time - startTime;
            cycleData.maxOpenness = maxOpenness;
            Debug.Log($"[{currentObject.name}] Distance start: {cycleData.initialDistance:F2}m, at30%: {cycleData.distanceAt30:F2}m, time: {cycleData.timeToGrab:F2}s, maxOpen: {cycleData.maxOpenness:F1}%");

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

    /// <summary>
    /// Sets the countdown background size to a fixed 6f x 3f and repositions it so that its center aligns with the center of the floating text.
    /// </summary>
    private void UpdateCountdownBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            // Set the fixed size.
            bgRect.sizeDelta = new Vector2(6f, 3f);
            // Adjust the background position so its center aligns with the visible text.
            // We use the textBounds.center from the TextMeshPro component.
            Vector2 textCenter = resultsPopupText.textBounds.center;
            // Since the text is top-aligned, the visible text center is offset from the text container’s center.
            // Shift the background vertically by the negative of the textBounds center y value.
            bgRect.anchoredPosition = new Vector2(bgRect.anchoredPosition.x, textCenter.y + 2f);
        }
    }

    /// <summary>
    /// Restores the background size for the popup.
    /// </summary>
    private void RestorePopupBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = originalBgSize;
        }
    }

    /// <summary>
    /// Sets the background color of the popup's parent image.
    /// </summary>
    private void SetPopupBackgroundColor(Color color) {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            bg.color = color;
        }
    }

    private float ComputePalmOpenness() {
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
        float minDistance = 0.02f;
        float maxDistance = 0.15f;
        float openness01 = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
        float palmOpennessPercent = openness01 * 100f;
        Debug.Log($"Palm Openness (Index/Thumb): {palmOpennessPercent:F2}%");
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

    /// <summary>
    /// Updates the background size for the final results popup to be slightly larger than the text.
    /// </summary>
    private void UpdateResultsPopupBackgroundSize() {
        Image bg = resultsPopupText.GetComponentInParent<Image>();
        if (bg != null) {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(10f, 7f);
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
                $"Initial Distance(m): <color=orange>{data.initialDistance:F2}</color>, " +
                $"Distance at 30% Openness: <color=orange>{data.distanceAt30:F2}</color>";
            lines.Add(line);
        }
        string finalText = string.Join("\n\n", lines);
        resultsPopupText.text = finalText;
        UpdateResultsPopupBackgroundSize();
    }

    private void RestartScene() {
        //restartButtonWrapper.SetActive(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        foreach (var obj in targetObjects) if (obj != null && !obj.activeSelf) obj.SetActive(true);
    }
}