using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class Stage2CycleController : MonoBehaviour {
    [Header("UI / Flow")]
    [SerializeField] private Stage2FloatingTimerUI timerUI;
    [SerializeField] private RehabMenuController rehabMenuController;
    [SerializeField] private Stage2ScoreUI scoreUI;

    [Header("Data Logging")]
    [SerializeField] private Stage2HandDataLogger dataLogger;

    [Header("Grab Detection")]
    [SerializeField] private TouchHandGrabInteractor rightHandInteractor;
    [SerializeField] private TouchHandGrabInteractor leftHandInteractor;

    [Header("Pause Settings")]
    [SerializeField] private float timeoutPauseSeconds = 2f;
    [SerializeField] private float successPauseSeconds = 2f;
    [SerializeField] private float showRedZeroSeconds = 0.2f;

    [Header("Hold Settings")]
    [SerializeField] private float touchLossGraceSeconds = 0.5f;

    [Header("Spawn Reference")]
    [SerializeField] private Transform spawnReference;

    [Header("Distance Presets")]
    [SerializeField] private float nearDistance = 0.35f;
    [SerializeField] private float mediumDistance = 0.55f;
    [SerializeField] private float farDistance = 0.75f;

    [Header("Random Spawn Area")]
    [SerializeField] private float horizontalSpread = 0.18f;
    [SerializeField] private float verticalSpread = 0.12f;

    [Header("Size Presets")]
    [SerializeField] private float smallScaleMultiplier = 0.8f;
    [SerializeField] private float mediumScaleMultiplier = 1f;
    [SerializeField] private float largeScaleMultiplier = 1.2f;

    [SerializeField] private GameObject backToMenuButton;

    private Stage2Config config;
    private Transform objectsParent;
    private OVRSkeleton handSkeleton;

    private List<GameObject> allObjects = new List<GameObject>();
    private Queue<GameObject> pool = new Queue<GameObject>();
    private readonly Dictionary<GameObject, Vector3> originalLocalScales = new Dictionary<GameObject, Vector3>();

    private GameObject currentObject;
    private int cyclesCompleted;
    private int score;

    private bool running;
    private bool waitingForGrab;
    private bool cycleSucceeded;

    private float cycleStartTime;
    private float currentContinuousTouchSeconds;
    private float maxContinuousTouchSeconds;

    private Coroutine cycleCoroutine;

    private void Awake() {
        if (scoreUI != null)
            scoreUI.gameObject.SetActive(false);

        if (backToMenuButton != null)
            backToMenuButton.SetActive(false);
    }

    public void BeginStage(Stage2Config cfg, Transform parent, OVRSkeleton skeleton) {
        config = cfg;
        objectsParent = parent;
        handSkeleton = skeleton;

        FileLogger.Log(
            $"Stage2 BeginStage called | hand={config.handSelection}, useTimeLimit={config.useTimeLimit}, timeLimitSeconds={config.timeLimitSeconds}, trials={config.trialsAmount}, endless={config.endlessMode}, touchMode={config.touchMode}, holdDuration={config.holdDurationSeconds}"
        );

        if (scoreUI != null)
            scoreUI.gameObject.SetActive(true);

        if (objectsParent == null) {
            FileLogger.Log("Stage2CycleController: objectsParent is null.");
            return;
        }

        if (handSkeleton == null) {
            FileLogger.Log("Stage2CycleController: handSkeleton is null.");
            return;
        }

        if (spawnReference == null) {
            FileLogger.Log("Stage2CycleController: spawnReference is null.");
            return;
        }

        if (config.handSelection == HandSelection.Right && rightHandInteractor == null) {
            FileLogger.Log("Stage2CycleController: rightHandInteractor is null.");
            return;
        }

        if (config.handSelection == HandSelection.Left && leftHandInteractor == null) {
            FileLogger.Log("Stage2CycleController: leftHandInteractor is null.");
            return;
        }

        if (config.handSelection == HandSelection.Both &&
            rightHandInteractor == null &&
            leftHandInteractor == null) {
            FileLogger.Log("Stage2CycleController: both hand interactors are null.");
            return;
        }

        objectsParent.gameObject.SetActive(true);

        allObjects = objectsParent.Cast<Transform>().Select(t => t.gameObject).ToList();
        FileLogger.Log($"Stage2 objectsParent={objectsParent.name}, childCount={objectsParent.childCount}, allObjects={allObjects.Count}");

        foreach (var obj in allObjects) {
            obj.SetActive(false);

            if (!originalLocalScales.ContainsKey(obj))
                originalLocalScales[obj] = obj.transform.localScale;

            var effect = obj.GetComponent<Stage2TouchDisappear>();
            if (effect == null)
                effect = obj.AddComponent<Stage2TouchDisappear>();

            effect.Initialize(handSkeleton, this);
        }

        if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        if (dataLogger != null)
            dataLogger.BeginSession(config, handSkeleton);
        else
            FileLogger.Log("Stage2CycleController: dataLogger is not assigned.");

        pool.Clear();
        FillPool();

        running = true;
        waitingForGrab = false;
        cyclesCompleted = 0;
        score = 0;
        currentObject = null;

        if (backToMenuButton != null)
            backToMenuButton.SetActive(config.endlessMode);

        UpdateScoreUI();

        StartNextCycle();
    }

    public void BackToMenuFromEndlessMode() {
        FileLogger.Log("Stage2 BackToMenuFromEndlessMode() called");
        EndStage();
    }

    private void FillPool() {
        var shuffled = allObjects.OrderBy(_ => Random.value).ToList();

        foreach (var obj in shuffled)
            pool.Enqueue(obj);

        FileLogger.Log("Stage2CycleController: Pool refilled and shuffled.");
    }

    private void StartNextCycle() {
        if (!running) return;

        if (!config.endlessMode && cyclesCompleted >= config.trialsAmount) {
            EndStage();
            return;
        }

        if (allObjects.Count == 0) {
            FileLogger.Log("Stage2CycleController: No objects found.");
            EndStage();
            return;
        }

        if (pool.Count == 0)
            FillPool();

        foreach (var obj in allObjects)
            obj.SetActive(false);

        currentObject = pool.Dequeue();
        FileLogger.Log($"Stage2 spawning object: {currentObject.name} at {GetRandomSpawnPosition()}");
        PrepareObjectForCurrentSettings(currentObject);

        cycleSucceeded = false;
        currentContinuousTouchSeconds = 0f;
        maxContinuousTouchSeconds = 0f;
        cycleStartTime = Time.time;

        currentObject.SetActive(true);

        int cycleIndex = cyclesCompleted;

        if (dataLogger != null)
            dataLogger.BeginCycle(cycleIndex, currentObject);

        FileLogger.Log($"Stage2 starting cycle {cycleIndex} with object {currentObject.name}");

        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);

        cycleCoroutine = StartCoroutine(RunCycle());
    }

    private void PrepareObjectForCurrentSettings(GameObject obj) {
        obj.transform.position = GetRandomSpawnPosition();

        if (originalLocalScales.TryGetValue(obj, out Vector3 baseScale))
            obj.transform.localScale = baseScale * GetScaleMultiplierForCurrentSetting();
    }

    private Vector3 GetRandomSpawnPosition() {
        float distance = GetDistanceForCurrentSetting();

        Vector3 basePos = spawnReference.position + spawnReference.forward * distance;

        Vector3 offset =
            spawnReference.right * Random.Range(-horizontalSpread, horizontalSpread) +
            spawnReference.up * Random.Range(-verticalSpread, verticalSpread);

        return basePos + offset + Vector3.down * 0.15f;
    }

    private float GetDistanceForCurrentSetting() {
        switch (config.targetDistance) {
            case TargetDistance.Near: return nearDistance;
            case TargetDistance.Medium: return mediumDistance;
            case TargetDistance.Far: return farDistance;
            case TargetDistance.Random: return Random.Range(nearDistance, farDistance);
            default: return mediumDistance;
        }
    }

    private float GetScaleMultiplierForCurrentSetting() {
        switch (config.targetSize) {
            case TargetSize.Small: return smallScaleMultiplier;
            case TargetSize.Medium: return mediumScaleMultiplier;
            case TargetSize.Large: return largeScaleMultiplier;
            default: return mediumScaleMultiplier;
        }
    }

    private IEnumerator RunCycle() {
        waitingForGrab = true;

        if (config.touchMode == TouchMode.Instant)
            yield return RunInstantCycle();
        else
            yield return RunHoldCycle();

        waitingForGrab = false;

        if (!running)
            yield break;

        if (cycleSucceeded) {
            CompleteCycleSuccess();

            yield return StartCoroutine(ForceReleaseGrabIfNeeded());

            yield return new WaitForSeconds(successPauseSeconds);
            StartNextCycle();
        }
    }

    private IEnumerator RunInstantCycle() {
        if (!config.useTimeLimit) {
            if (timerUI != null) {
                timerUI.Hide();
                timerUI.gameObject.SetActive(false);
            }

            while (!IsCurrentObjectGrabbed() && running) {
                SampleData();
                yield return null;
            }

            if (!running) yield break;

            cycleSucceeded = true;
            yield break;
        }

        float remaining = config.timeLimitSeconds;

        if (timerUI != null)
            timerUI.SetTime(remaining);

        while (remaining > 0f && !IsCurrentObjectGrabbed() && running) {
            remaining -= Time.deltaTime;

            if (timerUI != null)
                timerUI.SetTime(Mathf.Max(remaining, 0f));

            SampleData();
            yield return null;
        }

        if (!running) yield break;

        if (IsCurrentObjectGrabbed()) {
            cycleSucceeded = true;
            yield break;
        }

        yield return TimeoutCycle();
    }

    private IEnumerator RunHoldCycle() {
        float holdProgress = 0f;
        float untouchedTime = 0f;
        float remaining = config.timeLimitSeconds;
        bool wasGrabbingPreviously = false;

        if (config.useTimeLimit && timerUI != null)
            timerUI.SetTime(remaining);
        else if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        while (running) {
            bool isGrabbing = IsCurrentObjectGrabbed();

            if (isGrabbing) {
                holdProgress += Time.deltaTime;
                currentContinuousTouchSeconds = holdProgress;
                maxContinuousTouchSeconds = Mathf.Max(maxContinuousTouchSeconds, holdProgress);

                untouchedTime = 0f;
                wasGrabbingPreviously = true;
            }
            else {
                currentContinuousTouchSeconds = 0f;

                if (wasGrabbingPreviously)
                    untouchedTime += Time.deltaTime;

                bool shouldResumeTimer = !wasGrabbingPreviously || untouchedTime > touchLossGraceSeconds;

                if (untouchedTime > touchLossGraceSeconds) {
                    holdProgress = 0f;
                    wasGrabbingPreviously = false;
                }

                if (config.useTimeLimit && shouldResumeTimer) {
                    remaining -= Time.deltaTime;

                    if (timerUI != null)
                        timerUI.SetTime(Mathf.Max(remaining, 0f));

                    if (remaining <= 0f) {
                        yield return TimeoutCycle();
                        yield break;
                    }
                }
            }

            SampleData();

            if (holdProgress >= config.holdDurationSeconds) {
                cycleSucceeded = true;
                yield break;
            }

            yield return null;
        }
    }

    private bool IsCurrentObjectGrabbed() {
        if (!waitingForGrab)
            return false;

        if (currentObject == null)
            return false;

        return IsSelectedHandGrabbingCurrentObject();
    }

    private bool IsInteractorGrabbingCurrentObject(TouchHandGrabInteractor interactor) {
        if (interactor == null)
            return false;

        if (interactor.State != InteractorState.Select)
            return false;

        var selectedInteractable = interactor.Interactable;
        if (selectedInteractable == null)
            return false;

        Component selectedComponent = selectedInteractable as Component;
        if (selectedComponent == null)
            return false;

        Transform selectedTransform = selectedComponent.transform;
        Transform currentTransform = currentObject.transform;

        if (selectedTransform == currentTransform)
            return true;

        if (selectedTransform.IsChildOf(currentTransform))
            return true;

        if (currentTransform.IsChildOf(selectedTransform))
            return true;

        return false;
    }

    private IEnumerator TimeoutCycle() {
        cycleSucceeded = false;

        float totalTime = Time.time - cycleStartTime;

        if (timerUI != null)
            timerUI.SetExpiredZero();

        if (currentObject != null)
            currentObject.SetActive(false);

        if (dataLogger != null)
            dataLogger.EndCycle("Timeout", totalTime, maxContinuousTouchSeconds);

        FileLogger.Log($"Stage2 timeout on object {currentObject.name}");

        cyclesCompleted++;

        UpdateScoreUI();

        if (showRedZeroSeconds > 0f)
            yield return new WaitForSeconds(showRedZeroSeconds);

        if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        if (timeoutPauseSeconds > showRedZeroSeconds)
            yield return new WaitForSeconds(timeoutPauseSeconds - showRedZeroSeconds);

        StartNextCycle();
    }

    private void CompleteCycleSuccess() {
        float totalTime = Time.time - cycleStartTime;

        if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        if (dataLogger != null)
            dataLogger.EndCycle("Success", totalTime, maxContinuousTouchSeconds);

        cyclesCompleted++;
        score++;

        UpdateScoreUI();

        var effect = currentObject != null ? currentObject.GetComponent<Stage2TouchDisappear>() : null;

        if (effect != null)
            effect.PlaySuccessEffect();
        else if (currentObject != null)
            currentObject.SetActive(false);

        FileLogger.Log($"Stage2 cycle success. score={score}, cyclesCompleted={cyclesCompleted}");
    }

    private void UpdateScoreUI() {
        if (scoreUI == null)
            return;

        scoreUI.Show();

        if (config != null && config.endlessMode)
            scoreUI.SetScore(score, 0, true);
        else
            scoreUI.SetScore(score, config != null ? config.trialsAmount : 0, false);
    }

    private IEnumerator ForceReleaseGrabIfNeeded() {
        bool rightSelected = rightHandInteractor != null &&
                             rightHandInteractor.State == InteractorState.Select;

        bool leftSelected = leftHandInteractor != null &&
                            leftHandInteractor.State == InteractorState.Select;

        if (!rightSelected && !leftSelected)
            yield break;

        if (rightSelected)
            rightHandInteractor.enabled = false;

        if (leftSelected)
            leftHandInteractor.enabled = false;

        yield return null;
        yield return null;

        if (rightHandInteractor != null)
            rightHandInteractor.enabled = true;

        if (leftHandInteractor != null)
            leftHandInteractor.enabled = true;

        yield return null;
    }

    private void SampleData() {
        if (dataLogger == null)
            return;

        bool isGrabbing = IsCurrentObjectGrabbed();
        dataLogger.SampleTrajectory(isGrabbing, currentContinuousTouchSeconds);
    }

    public void SetCurrentObjectTouchState(GameObject obj, bool isTouching) {
        // Kept only for compatibility with old scripts.
        // Stage 2 now uses TouchHandGrabInteractor directly.
    }

    private bool IsSelectedHandGrabbingCurrentObject() {
        if (config == null)
            return false;

        switch (config.handSelection) {
            case HandSelection.Right:
                return IsInteractorGrabbingCurrentObject(rightHandInteractor);

            case HandSelection.Left:
                return IsInteractorGrabbingCurrentObject(leftHandInteractor);

            case HandSelection.Both:
                return IsInteractorGrabbingCurrentObject(rightHandInteractor) ||
                       IsInteractorGrabbingCurrentObject(leftHandInteractor);

            default:
                return false;
        }
    }

    public void EndStage() {
        FileLogger.Log("Stage2 EndStage() ENTERED");

        running = false;
        waitingForGrab = false;

        if (cycleCoroutine != null) {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        if (dataLogger != null)
            dataLogger.EndSessionScore(score, cyclesCompleted);

        // Force release interactors
        if (rightHandInteractor != null)
            rightHandInteractor.enabled = false;

        if (leftHandInteractor != null)
            leftHandInteractor.enabled = false;

        if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        if (backToMenuButton != null)
            backToMenuButton.SetActive(false);

        if (scoreUI != null)
            scoreUI.gameObject.SetActive(false);

        // Hide all objects FIRST
        if (currentObject != null)
            currentObject.SetActive(false);

        foreach (var obj in allObjects) {
            if (obj == null)
                continue;

            if (originalLocalScales.TryGetValue(obj, out Vector3 baseScale))
                obj.transform.localScale = baseScale;

            obj.SetActive(false);
        }

        // Then hide parent
        if (objectsParent != null)
            objectsParent.gameObject.SetActive(false);

        // Re-enable interactors
        if (rightHandInteractor != null)
            rightHandInteractor.enabled = true;

        if (leftHandInteractor != null)
            leftHandInteractor.enabled = true;

        if (rehabMenuController != null)
            rehabMenuController.EnterStage2Settings();
        else
            FileLogger.Log("Stage2 EndStage: rehabMenuController is NULL");

        FileLogger.Log("Stage2 EndStage() FINISHED");
    }
}