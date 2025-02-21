using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Oculus.Interaction.HandGrab; // For TouchHandGrabInteractor
using UnityEngine.UI;               // For Image
using Oculus.Interaction;           // For InteractorState

public class OVRHandCountdownCycle : MonoBehaviour {
    [Header("Target Objects")]
    [Tooltip("Assign the objects to be picked up in order.")]
    public GameObject[] targetObjects;

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

    // Store the original background size.
    private Vector2 originalBgSize;

    // Cycle counter and storage.
    private int currentCycle = 0;
    private List<CycleData> cycleDataList = new List<CycleData>();

    // Structure to hold per-cycle metrics.
    private class CycleData {
        public string objectName;
        public float timeToGrab;       // Time (in seconds) from text disappearance until grab.
        public float maxOpenness;      // Maximum palm openness (% 0–100) during the cycle.
        public float initialOpenness;  // Palm openness (% 0–100) measured immediately when text disappears.
        public float initialDistance;  // Distance (in meters) from the wrist to the object at cycle start.
        public float distanceAt30;     // Distance (in meters) from the wrist to the object when palm openness first exceeds 30%.
    }

    private void Start() {
        if (handSkeleton == null) handSkeleton = GetComponent<OVRSkeleton>();
        if (hand == null) hand = GetComponent<OVRHand>();

        if (resultsPopupText == null) {
            Debug.LogWarning("Results Popup Text is not assigned in the Inspector!");
        }
        else {
            // Center the text.
            RectTransform textRect = resultsPopupText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;

            // Store the original background size.
            Image bg = resultsPopupText.GetComponentInParent<Image>();
            if (bg != null) {
                RectTransform bgRect = bg.GetComponent<RectTransform>();
                originalBgSize = bgRect.sizeDelta;
            }
        }

        // Begin the cycle coroutine.
        StartCoroutine(CycleCoroutine());
    }

    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            GameObject currentObject = targetObjects[currentCycle];
            CycleData cycleData = new CycleData();
            cycleData.objectName = currentObject.name;

            // --- Countdown Sequence Setup ---
            // Increase countdown text size (3x) and use a fixed small background.
            float originalFontSize = resultsPopupText.fontSize;
            float countdownFontSize = originalFontSize * 3f;
            resultsPopupText.fontSize = countdownFontSize;
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0.7f));

            // For each countdown step, update the text and re-center the background.
            resultsPopupText.text = "3";
            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(1f);

            resultsPopupText.text = "2";
            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(1f);

            resultsPopupText.text = "1";
            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(1f);

            resultsPopupText.text = "Pick up the " + currentObject.name + "!";
            UpdateCountdownBackgroundSize();
            yield return new WaitForSeconds(2.5f);

            // Clear countdown text, revert font size, and restore background.
            resultsPopupText.text = "";
            SetPopupBackgroundColor(new Color(0f, 0f, 0f, 0f));
            resultsPopupText.fontSize = originalFontSize;
            RestorePopupBackgroundSize();

            // --- Immediately After Countdown: Record Initial Metrics ---
            cycleData.initialOpenness = ComputePalmOpenness();
            Transform wrist = GetWristTransform();
            if (wrist != null) {
                cycleData.initialDistance = Vector3.Distance(wrist.position, currentObject.transform.position);
            }
            else {
                cycleData.initialDistance = 0f;
            }

            // --- Start Tracking Cycle Data ---
            float cycleStartTime = Time.time;
            float maxOpenness = cycleData.initialOpenness;
            bool distanceAt30Recorded = false;

            if (rightHandInteractor == null) {
                yield break;
            }

            while (rightHandInteractor.State != InteractorState.Select) {
                float currentOpenness = ComputePalmOpenness();
                if (currentOpenness > maxOpenness)
                    maxOpenness = currentOpenness;
                if (!distanceAt30Recorded && currentOpenness >= 30f) {
                    wrist = GetWristTransform();
                    if (wrist != null) {
                        cycleData.distanceAt30 = Vector3.Distance(wrist.position, currentObject.transform.position);
                        distanceAt30Recorded = true;
                    }
                }
                yield return null;
            }

            cycleData.timeToGrab = Time.time - cycleStartTime;
            cycleData.maxOpenness = maxOpenness;
            Debug.Log(
                $"[{currentObject.name}] Cycle Metrics:\n" +
                $"Initial Distance: {cycleData.initialDistance:F2} m\n" +
                $"Distance at 30% Openness: {cycleData.distanceAt30:F2} m\n" +
                $"Time to Grab: {cycleData.timeToGrab:F2} sec\n" +
                $"Max Palm Openness: {cycleData.maxOpenness:F1}%"
            );

            float releaseTimeout = 2f;
            float releaseTimer = 0f;
            while (rightHandInteractor.State == InteractorState.Select && releaseTimer < releaseTimeout) {
                yield return null;
                releaseTimer += Time.deltaTime;
            }

            if (releaseTimer >= releaseTimeout) {
                Debug.LogWarning($"Timeout waiting for release of {currentObject.name}. Continuing to next cycle.");
            }

            cycleDataList.Add(cycleData);
            currentCycle++;
            yield return new WaitForSeconds(1f);
        }

        // Remove target objects from the scene.
        foreach (GameObject obj in targetObjects) {
            if (obj != null)
                obj.SetActive(false);
        }
        // Also remove the table.
        if (table != null)
            table.SetActive(false);

        // Before showing results, set background to semi-transparent black.
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
}
