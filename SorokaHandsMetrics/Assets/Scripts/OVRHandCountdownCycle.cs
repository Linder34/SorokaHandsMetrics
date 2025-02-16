using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Oculus.Interaction.HandGrab; // For TouchHandGrabInteractor
using UnityEngine.UI;               // For Image and CanvasScaler
using Oculus.Interaction;           // For InteractorState

public class OVRHandCountdownCycle : MonoBehaviour {
    [Header("Target Objects")]
    [Tooltip("Assign the objects to be picked up in order.")]
    public GameObject[] targetObjects;

    [Header("Floating Text Settings")]
    [Tooltip("Distance from the camera where the floating text is displayed.")]
    public float textDistance = 2.0f;
    [Tooltip("Optional plane to hide after all cycles.")]
    public GameObject plane;

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

    [Header("Results Popup")]
    [Tooltip("Assign the TextMeshProUGUI element (already in your scene) that will display the results.")]
    public TextMeshProUGUI resultsPopupText;

    [Header("Countdown Canvas")]
    [Tooltip("Assign the Canvas used for countdown (its GameObject should have an Image component for background).")]
    public Canvas countdownCanvas;

    // Internal UI reference for the floating text.
    private TextMeshProUGUI instructionText;
    // We'll store the floating text canvas transform so we can update its position.
    private Transform floatingCanvasTransform;

    // Cycle counter and storage.
    private int currentCycle = 0;
    private List<CycleData> cycleDataList = new List<CycleData>();

    // Structure to hold per-cycle metrics.
    private class CycleData {
        public string objectName;
        public float timeToGrab;       // Time (in seconds) from text disappearance until grab.
        public float maxOpenness;      // Maximum palm openness (% 0–100) during the cycle.
        public float initialOpenness;  // Palm openness (%) measured immediately when text disappears.
        public float initialDistance;  // Distance (in meters) from the wrist to the object at cycle start.
        public float distanceAt30;     // Distance (in meters) from the wrist to the object when palm openness first exceeds 30%.
    }

    private void Start() {
        if (handSkeleton == null) handSkeleton = GetComponent<OVRSkeleton>();
        if (hand == null) hand = GetComponent<OVRHand>();

        // Create the floating text UI element attached to the main camera.
        instructionText = CreateFloatingText(Camera.main.transform, textDistance);

        // Begin the cycle coroutine.
        StartCoroutine(CycleCoroutine());
    }

    private void Update() {
        if (Camera.main != null) {
            Transform cam = Camera.main.transform;
            if (floatingCanvasTransform != null) {
                floatingCanvasTransform.position = cam.position + cam.forward * textDistance;
                floatingCanvasTransform.rotation = cam.rotation;
            }
        }
    }

    private IEnumerator CycleCoroutine() {
        while (currentCycle < targetObjects.Length) {
            GameObject currentObject = targetObjects[currentCycle];
            CycleData cycleData = new CycleData();
            cycleData.objectName = currentObject.name;

            // Set the countdown canvas background to semi-transparent black at the start of each cycle.
            if (countdownCanvas != null) {
                Image bg = countdownCanvas.GetComponent<Image>();
                if (bg != null) {
                    bg.color = new Color(0f, 0f, 0f, 0.7f);
                }
            }

            // Ensure the floating text is enabled.
            instructionText.enabled = true;

            // --- Countdown Sequence ---
            instructionText.text = "3";
            yield return new WaitForSeconds(1f);
            instructionText.text = "2";
            yield return new WaitForSeconds(1f);
            instructionText.text = "1";
            yield return new WaitForSeconds(1f);
            instructionText.text = "Pick up the " + currentObject.name + "!";
            yield return new WaitForSeconds(1f); // Display prompt for 1 second.
            instructionText.text = "";          // Then clear the text.

            // After clearing the text, set the canvas background to transparent.
            if (countdownCanvas != null) {
                Image bg = countdownCanvas.GetComponent<Image>();
                if (bg != null) {
                    bg.color = new Color(0f, 0f, 0f, 0f);
                }
            }

            // --- Immediately After Text Disappears: Record Initial Metrics ---
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

        foreach (GameObject obj in targetObjects) {
            if (obj != null)
                obj.SetActive(false);
        }
        if (plane != null)
            plane.SetActive(false);

        // Before showing results, set the countdown canvas background back to semi-transparent black.
        if (countdownCanvas != null) {
            Image bg = countdownCanvas.GetComponent<Image>();
            if (bg != null) {
                bg.color = new Color(0f, 0f, 0f, 0.7f);
            }
        }
        ShowResultsPopup();
    }

    private TextMeshProUGUI CreateFloatingText(Transform parent, float distance) {
        GameObject canvasObj = new GameObject("FloatingCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 5000f;

        canvasObj.transform.SetParent(parent);
        floatingCanvasTransform = canvasObj.transform;
        canvasObj.transform.localPosition = new Vector3(0, 0, distance);
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * 0.001f;

        GameObject textObj = new GameObject("InstructionText");
        textObj.transform.SetParent(canvasObj.transform);
        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.fontSize = 5;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;

        RectTransform rectTransform = tmpText.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        rectTransform.anchoredPosition3D = Vector3.zero;

        tmpText.text = "";
        return tmpText;
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

    private void ShowResultsPopup() {
        if (resultsPopupText == null) {
            Debug.LogWarning("Results Popup Text is not assigned in the Inspector!");
            return;
        }
        Image panelImage = resultsPopupText.GetComponentInParent<Image>();
        if (panelImage != null) {
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);
        }
        List<string> lines = new List<string>();
        foreach (CycleData data in cycleDataList) {
            string line =
                $"<color=#FF69B4>{data.objectName}</color>:\n" +
                $"Total Time(s): <color=orange>{data.timeToGrab:F2}</color>, " +
                $"MaxOpenness(%): <color=orange>{data.maxOpenness:F1}</color>, " +
                $"Initial Distance(m): <color=orange>{data.initialDistance:F2}</color>, " +
                $"Open Palm Distance: <color=orange>{data.distanceAt30:F2}</color>";
            lines.Add(line);
        }
        string finalText = string.Join("\n\n", lines);
        resultsPopupText.text = finalText;
    }
}
