using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Stage1CycleController : MonoBehaviour {
    [Header("UI / Flow")]
    [SerializeField] private Stage1FloatingTimerUI timerUI;
    [SerializeField] private RehabMenuController rehabMenuController;
    [SerializeField] private Stage1ScoreUI scoreUI;

    [Header("Data Logging")]
    [SerializeField] private Stage1HandDataLogger dataLogger;

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

    private Stage1Config config;
    private Transform objectsParent;
    private OVRSkeleton handSkeleton;

    private List<GameObject> allObjects = new List<GameObject>();
    private Queue<GameObject> pool = new Queue<GameObject>();
    private readonly Dictionary<GameObject, Vector3> originalLocalScales = new Dictionary<GameObject, Vector3>();

    private GameObject currentObject;
    private int cyclesCompleted;
    private int score;

    private bool running;
    private bool waitingForTouch;

    private bool isCurrentObjectTouched;
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

    public void BeginStage(Stage1Config cfg, Transform parent, OVRSkeleton skeleton) {
        config = cfg;
        objectsParent = parent;
        handSkeleton = skeleton;

        FileLogger.Log(
            $"BeginStage called | hand={config.handSelection}, useTimeLimit={config.useTimeLimit}, timeLimitSeconds={config.timeLimitSeconds}, trials={config.trialsAmount}, endless={config.endlessMode}, touchMode={config.touchMode}, holdDuration={config.holdDurationSeconds}"
        );

        if (scoreUI != null)
            scoreUI.gameObject.SetActive(true);

        if (objectsParent == null) {
            FileLogger.Log("Stage1CycleController: objectsParent is null.");
            return;
        }

        if (handSkeleton == null) {
            FileLogger.Log("Stage1CycleController: handSkeleton is null.");
            return;
        }

        if (spawnReference == null) {
            FileLogger.Log("Stage1CycleController: spawnReference is null.");
            return;
        }

        objectsParent.gameObject.SetActive(true);

        allObjects = objectsParent.Cast<Transform>().Select(t => t.gameObject).ToList();
        //originalLocalScales.Clear();

        foreach (var obj in allObjects) {
            obj.SetActive(false);

            if (!originalLocalScales.ContainsKey(obj))
                originalLocalScales[obj] = obj.transform.localScale;

            var touch = obj.GetComponent<Stage1TouchDisappear>();
            if (touch == null)
                touch = obj.AddComponent<Stage1TouchDisappear>();

            touch.Initialize(handSkeleton, this);
        }

        if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        if (dataLogger != null)
            dataLogger.BeginSession(config, handSkeleton);
        else
            FileLogger.Log("Stage1CycleController: dataLogger is not assigned.");

        pool.Clear();
        FillPool();

        running = true;
        waitingForTouch = false;
        cyclesCompleted = 0;
        score = 0;
        if (backToMenuButton != null)
            backToMenuButton.SetActive(config.endlessMode);
        currentObject = null;

        UpdateScoreUI();

        StartNextCycle();
    }
    public void BackToMenuFromEndlessMode() {
        FileLogger.Log("BackToMenuFromEndlessMode() called");

        EndStage();
    }


    private void FillPool() {
        var shuffled = allObjects.OrderBy(_ => Random.value).ToList();

        foreach (var obj in shuffled)
            pool.Enqueue(obj);

        FileLogger.Log("Stage1CycleController: Pool refilled and shuffled.");
    }

    private void StartNextCycle() {
        if (!running) return;

        if (!config.endlessMode && cyclesCompleted >= config.trialsAmount) {
            EndStage();
            return;
        }

        if (allObjects.Count == 0) {
            FileLogger.Log("Stage1CycleController: No objects found.");
            EndStage();
            return;
        }

        if (pool.Count == 0)
            FillPool();

        foreach (var obj in allObjects)
            obj.SetActive(false);

        currentObject = pool.Dequeue();
        PrepareObjectForCurrentSettings(currentObject);

        isCurrentObjectTouched = false;
        cycleSucceeded = false;
        currentContinuousTouchSeconds = 0f;
        maxContinuousTouchSeconds = 0f;
        cycleStartTime = Time.time;

        currentObject.SetActive(true);

        int cycleIndex = cyclesCompleted;

        if (dataLogger != null)
            dataLogger.BeginCycle(cycleIndex, currentObject);

        FileLogger.Log($"Starting cycle {cycleIndex} with object {currentObject.name}");

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

        float baseDownOffset = 0.15f;
        float largeBalloonExtraDownOffset = 0.12f;

        float downOffset = baseDownOffset;

        if (config != null && config.targetSize == TargetSize.Large)
            downOffset += largeBalloonExtraDownOffset;

        return basePos + offset + Vector3.down * downOffset;
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
        waitingForTouch = true;

        if (config.touchMode == TouchMode.Instant)
            yield return RunInstantCycle();
        else
            yield return RunHoldCycle();

        waitingForTouch = false;

        if (!running)
            yield break;

        if (cycleSucceeded) {
            CompleteCycleSuccess();
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

            while (!isCurrentObjectTouched && running) {
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

        while (remaining > 0f && !isCurrentObjectTouched && running) {
            remaining -= Time.deltaTime;

            if (timerUI != null)
                timerUI.SetTime(Mathf.Max(remaining, 0f));

            SampleData();
            yield return null;
        }

        if (!running) yield break;

        if (isCurrentObjectTouched) {
            cycleSucceeded = true;
            yield break;
        }

        yield return TimeoutCycle();
    }

    private IEnumerator RunHoldCycle() {
        float holdProgress = 0f;
        float untouchedTime = 0f;
        float remaining = config.timeLimitSeconds;
        bool wasTouchingPreviously = false;

        if (config.useTimeLimit && timerUI != null)
            timerUI.SetTime(remaining);
        else if (timerUI != null) {
            timerUI.Hide();
            timerUI.gameObject.SetActive(false);
        }

        while (running) {
            if (isCurrentObjectTouched) {
                holdProgress += Time.deltaTime;
                currentContinuousTouchSeconds = holdProgress;
                maxContinuousTouchSeconds = Mathf.Max(maxContinuousTouchSeconds, holdProgress);

                untouchedTime = 0f;
                wasTouchingPreviously = true;
            }
            else {
                currentContinuousTouchSeconds = 0f;

                if (wasTouchingPreviously)
                    untouchedTime += Time.deltaTime;

                bool shouldResumeTimer = !wasTouchingPreviously || untouchedTime > touchLossGraceSeconds;

                if (untouchedTime > touchLossGraceSeconds) {
                    holdProgress = 0f;
                    wasTouchingPreviously = false;
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

    private IEnumerator TimeoutCycle() {
        cycleSucceeded = false;

        float totalTime = Time.time - cycleStartTime;

        if (timerUI != null)
            timerUI.SetExpiredZero();

        if (currentObject != null)
            currentObject.SetActive(false);

        if (dataLogger != null)
            dataLogger.EndCycle("Timeout", totalTime, maxContinuousTouchSeconds);

        FileLogger.Log($"Timeout on object {currentObject.name}");

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

        if (currentObject != null) {
            Stage1TouchDisappear touchDisappear = currentObject.GetComponent<Stage1TouchDisappear>();

            if (touchDisappear != null)
                touchDisappear.Pop();
            else
                currentObject.SetActive(false);
        }

        if (dataLogger != null)
            dataLogger.EndCycle("Success", totalTime, maxContinuousTouchSeconds);

        cyclesCompleted++;
        score++;

        UpdateScoreUI();

        FileLogger.Log($"Cycle success. score={score}, cyclesCompleted={cyclesCompleted}");
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

    private void ShowFinalScoreUI() {
        if (scoreUI == null)
            return;

        scoreUI.Show();

        if (config != null && config.endlessMode)
            scoreUI.SetFinalScore(score, 0, true);
        else
            scoreUI.SetFinalScore(score, config != null ? config.trialsAmount : cyclesCompleted, false);
    }

    private void SampleData() {
        if (dataLogger == null)
            return;

        dataLogger.SampleTrajectory(isCurrentObjectTouched, currentContinuousTouchSeconds);
    }

    public void SetCurrentObjectTouchState(GameObject obj, bool isTouching) {
        if (!running) return;
        if (!waitingForTouch) return;
        if (obj != currentObject) return;

        isCurrentObjectTouched = isTouching;
    }

    public void EndStage() {
        FileLogger.Log("EndStage() ENTERED");

        running = false;
        waitingForTouch = false;

        if (cycleCoroutine != null) {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        // Log final session score
        if (dataLogger != null)
            dataLogger.EndSessionScore(score, cyclesCompleted);

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

        if (rehabMenuController != null)
            rehabMenuController.EnterStage1Settings();
        else
            FileLogger.Log("EndStage: rehabMenuController is NULL");

        FileLogger.Log("EndStage() FINISHED");
    }
}