using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Stage1SettingsUI : MonoBehaviour {

    [Header("Menu Controller")]
    [SerializeField] private RehabMenuController rehabMenuController;

    [Header("Stage 1 Runtime")]
    [SerializeField] private Stage1CycleController stage1CycleController;
    [SerializeField] private Transform stage1ObjectsParent;

    [Header("Hand Skeletons")]
    [SerializeField] private OVRSkeleton rightHandSkeleton;
    [SerializeField] private OVRSkeleton leftHandSkeleton;

    [Header("Current Config")]
    [SerializeField] private Stage1Config currentConfig = new Stage1Config();

    [Header("Sliders")]
    [SerializeField] private Slider trialsSlider;
    [SerializeField] private Slider timeLimitSlider;
    [SerializeField] private Slider holdDurationSlider;

    [Header("Value Labels")]
    [SerializeField] private TMP_Text trialsValueText;
    [SerializeField] private TMP_Text timeLimitValueText;
    [SerializeField] private TMP_Text holdDurationValueText;

    [Header("Hand Toggles")]
    [SerializeField] private Toggle handRightToggle;
    [SerializeField] private Toggle handLeftToggle;
    [SerializeField] private Toggle handBothToggle;

    [Header("Distance Toggles")]
    [SerializeField] private Toggle distanceNearToggle;
    [SerializeField] private Toggle distanceMediumToggle;
    [SerializeField] private Toggle distanceFarToggle;
    [SerializeField] private Toggle distanceRandomToggle;

    [Header("Size Toggles")]
    [SerializeField] private Toggle sizeSmallToggle;
    [SerializeField] private Toggle sizeMediumToggle;
    [SerializeField] private Toggle sizeLargeToggle;

    [Header("Other Toggles")]
    [SerializeField] private Toggle endlessModeToggle;
    [SerializeField] private Toggle timeLimitToggle;

    [Header("Touch Mode")]
    [SerializeField] private Toggle touchModeToggle;

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

    public void OnDistanceNearChanged(bool isOn) { if (isOn) currentConfig.targetDistance = TargetDistance.Near; }
    public void OnDistanceMediumChanged(bool isOn) { if (isOn) currentConfig.targetDistance = TargetDistance.Medium; }
    public void OnDistanceFarChanged(bool isOn) { if (isOn) currentConfig.targetDistance = TargetDistance.Far; }
    public void OnDistanceRandomChanged(bool isOn) { if (isOn) currentConfig.targetDistance = TargetDistance.Random; }

    public void OnTargetSizeSmallChanged(bool isOn) { if (isOn) currentConfig.targetSize = TargetSize.Small; }
    public void OnTargetSizeMediumChanged(bool isOn) { if (isOn) currentConfig.targetSize = TargetSize.Medium; }
    public void OnTargetSizeLargeChanged(bool isOn) { if (isOn) currentConfig.targetSize = TargetSize.Large; }

    public void OnTouchModeChanged(bool isOn) {
        currentConfig.touchMode = isOn ? TouchMode.Hold : TouchMode.Instant;
    }

    public void OnHoldDurationSecondsChanged(float value) {
        currentConfig.holdDurationSeconds = value;
        RefreshValueLabels();
    }

    #endregion

    #region UI Refresh

    private void RefreshValueLabels() {
        if (trialsValueText != null)
            trialsValueText.text = currentConfig.endlessMode ? "∞" : currentConfig.trialsAmount.ToString();

        if (timeLimitValueText != null)
            timeLimitValueText.text = Mathf.RoundToInt(currentConfig.timeLimitSeconds).ToString();

        if (holdDurationValueText != null)
            holdDurationValueText.text = currentConfig.holdDurationSeconds.ToString("F1");
    }

    #endregion

    #region Start Stage

    public void StartStage1() {
        currentConfig.ClampValues();

        OVRSkeleton selectedSkeleton = GetSelectedHandSkeleton();

        if (stage1CycleController != null) {
            stage1CycleController.BeginStage(currentConfig, stage1ObjectsParent, selectedSkeleton);
        }
        else {
            FileLogger.Log("Stage1CycleController NOT assigned!");
        }

        if (rehabMenuController != null) {
            rehabMenuController.StartStage1();
        }
    }

    private OVRSkeleton GetSelectedHandSkeleton() {
        switch (currentConfig.handSelection) {
            case HandSelection.Right:
                return rightHandSkeleton;

            case HandSelection.Left:
                return leftHandSkeleton;

            case HandSelection.Both:
                FileLogger.Log("Both hands selected (not supported yet), defaulting to Right.");
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
        currentConfig.timeLimitSeconds = 3;

        currentConfig.targetDistance = TargetDistance.Near;
        currentConfig.targetSize = TargetSize.Small;

        currentConfig.touchMode = TouchMode.Instant;
        currentConfig.holdDurationSeconds = 1f;

        RefreshValueLabels();
    }

    #endregion
}