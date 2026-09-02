using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Stage4SettingsUI : MonoBehaviour {
    [Header("Menu Controller")]
    [SerializeField] private RehabMenuController rehabMenuController;

    [Header("Stage 4 Runtime")]
    [SerializeField] private Stage4CycleController stage4CycleController;
    [SerializeField] private Transform stage4ObjectsParent;

    [Header("Hand Skeletons")]
    [SerializeField] private OVRSkeleton rightHandSkeleton;
    [SerializeField] private OVRSkeleton leftHandSkeleton;

    [Header("Current Config")]
    [SerializeField] private Stage4Config currentConfig = new Stage4Config();

    [Header("Sliders")]
    [SerializeField] private Slider trialsSlider;
    [SerializeField] private Slider timeLimitSlider;

    [Header("Value Labels")]
    [SerializeField] private TMP_Text trialsValueText;
    [SerializeField] private TMP_Text timeLimitValueText;

    [Header("Hand Toggles")]
    [SerializeField] private Toggle handRightToggle;
    [SerializeField] private Toggle handLeftToggle;
    [SerializeField] private Toggle handBothToggle;

    [Header("Distance Toggles")]
    [SerializeField] private Toggle distanceNearToggle;
    [SerializeField] private Toggle distanceMediumToggle;
    [SerializeField] private Toggle distanceFarToggle;
    [SerializeField] private Toggle distanceRandomToggle;

    [Header("Target Size Toggles")]
    [SerializeField] private Toggle sizeSmallToggle;
    [SerializeField] private Toggle sizeMediumToggle;
    [SerializeField] private Toggle sizeLargeToggle;

    [Header("Session Toggles")]
    [SerializeField] private Toggle endlessModeToggle;
    [SerializeField] private Toggle timeLimitToggle;

    [Header("Ring Size Toggles")]
    [SerializeField] private Toggle ringLargeToggle;
    [SerializeField] private Toggle ringMediumToggle;
    [SerializeField] private Toggle ringSmallToggle;

    [Header("Path Height Toggles")]
    [SerializeField] private Toggle pathLowToggle;
    [SerializeField] private Toggle pathMediumToggle;
    [SerializeField] private Toggle pathHighToggle;

    private void Start() {
        SetDefaults();
    }

    #region UI Callbacks

    public void OnHandRightChanged(bool isOn) {
        if (!isOn) return;
        currentConfig.handSelection = HandSelection.Right;
    }

    public void OnHandLeftChanged(bool isOn) {
        if (!isOn) return;
        currentConfig.handSelection = HandSelection.Left;
    }

    public void OnHandBothChanged(bool isOn) {
        if (!isOn) return;
        currentConfig.handSelection = HandSelection.Both;
    }

    public void OnEndlessModeChanged(bool isOn) {
        currentConfig.endlessMode = isOn;
        RefreshValueLabels();
    }

    public void OnTrialsAmountChanged(float value) {
        currentConfig.trialsAmount = Mathf.RoundToInt(value);
        RefreshValueLabels();
    }

    public void OnUseTimeLimitChanged(bool isOn) {
        currentConfig.useTimeLimit = isOn;
    }

    public void OnTimeLimitSecondsChanged(float value) {
        currentConfig.timeLimitSeconds = Mathf.Round(value);
        RefreshValueLabels();
    }

    public void OnDistanceNearChanged(bool isOn) {
        if (isOn) currentConfig.targetDistance = TargetDistance.Near;
    }

    public void OnDistanceMediumChanged(bool isOn) {
        if (isOn) currentConfig.targetDistance = TargetDistance.Medium;
    }

    public void OnDistanceFarChanged(bool isOn) {
        if (isOn) currentConfig.targetDistance = TargetDistance.Far;
    }

    public void OnDistanceRandomChanged(bool isOn) {
        if (isOn) currentConfig.targetDistance = TargetDistance.Random;
    }

    public void OnTargetSizeSmallChanged(bool isOn) {
        if (isOn) currentConfig.targetSize = TargetSize.Small;
    }

    public void OnTargetSizeMediumChanged(bool isOn) {
        if (isOn) currentConfig.targetSize = TargetSize.Medium;
    }

    public void OnTargetSizeLargeChanged(bool isOn) {
        if (isOn) currentConfig.targetSize = TargetSize.Large;
    }

    public void OnRingLargeChanged(bool isOn) {
        if (isOn) currentConfig.ringSize = Stage4RingSize.Large;
    }

    public void OnRingMediumChanged(bool isOn) {
        if (isOn) currentConfig.ringSize = Stage4RingSize.Medium;
    }

    public void OnRingSmallChanged(bool isOn) {
        if (isOn) currentConfig.ringSize = Stage4RingSize.Small;
    }

    public void OnPathLowChanged(bool isOn) {
        if (isOn) currentConfig.pathHeight = Stage4PathHeight.Low;
    }

    public void OnPathMediumChanged(bool isOn) {
        if (isOn) currentConfig.pathHeight = Stage4PathHeight.Medium;
    }

    public void OnPathHighChanged(bool isOn) {
        if (isOn) currentConfig.pathHeight = Stage4PathHeight.High;
    }

    #endregion

    #region UI Refresh

    private void RefreshValueLabels() {
        if (trialsValueText != null) {
            trialsValueText.text =
                currentConfig.endlessMode
                    ? "∞"
                    : currentConfig.trialsAmount.ToString();
        }

        if (timeLimitValueText != null) {
            timeLimitValueText.text =
                Mathf.RoundToInt(currentConfig.timeLimitSeconds).ToString();
        }
    }

    #endregion

    #region Start Stage

    public void StartStage4() {
        currentConfig.ClampValues();

        OVRSkeleton selectedSkeleton = GetSelectedHandSkeleton();

        if (stage4CycleController != null) {
            stage4CycleController.BeginStage(
                currentConfig,
                stage4ObjectsParent,
                selectedSkeleton);
        }
        else {
            FileLogger.Log("Stage4CycleController NOT assigned!");
        }

        if (rehabMenuController != null) {
            rehabMenuController.StartStage4();
        }
    }

    private OVRSkeleton GetSelectedHandSkeleton() {
        switch (currentConfig.handSelection) {
            case HandSelection.Right:
                return rightHandSkeleton;

            case HandSelection.Left:
                return leftHandSkeleton;

            case HandSelection.Both:
                FileLogger.Log(
                    "Both hands selected (not supported yet), defaulting to Right.");
                return rightHandSkeleton;

            default:
                return rightHandSkeleton;
        }
    }

    #endregion

    #region Defaults

    public void SetDefaults() {
        currentConfig.handSelection = HandSelection.Right;

        currentConfig.endlessMode = false;
        currentConfig.trialsAmount = 10;

        currentConfig.useTimeLimit = false;
        currentConfig.timeLimitSeconds = 10f;

        currentConfig.targetDistance = TargetDistance.Medium;
        currentConfig.targetSize = TargetSize.Medium;

        currentConfig.ringSize = Stage4RingSize.Large;
        currentConfig.pathHeight = Stage4PathHeight.Medium;

        if (handRightToggle != null)
            handRightToggle.isOn = true;

        if (handLeftToggle != null)
            handLeftToggle.isOn = false;

        if (handBothToggle != null)
            handBothToggle.isOn = false;

        if (distanceMediumToggle != null)
            distanceMediumToggle.isOn = true;

        if (sizeMediumToggle != null)
            sizeMediumToggle.isOn = true;

        if (ringLargeToggle != null)
            ringLargeToggle.isOn = true;

        if (ringMediumToggle != null)
            ringMediumToggle.isOn = false;

        if (ringSmallToggle != null)
            ringSmallToggle.isOn = false;

        if (pathLowToggle != null)
            pathLowToggle.isOn = false;

        if (pathMediumToggle != null)
            pathMediumToggle.isOn = true;

        if (pathHighToggle != null)
            pathHighToggle.isOn = false;

        RefreshValueLabels();

    }

    #endregion
}