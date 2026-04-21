using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Stage1CycleController : MonoBehaviour {
    [Header("Timer UI")]
    [SerializeField] private Stage1FloatingTimerUI timerUI;

    [Header("Timeout Settings")]
    [SerializeField] private float timeoutPauseSeconds = 2f;
    [SerializeField] private float showRedZeroSeconds = 0.2f;

    [Header("Spawn Reference")]
    [Tooltip("Center point from which Near / Medium / Far positions are generated.")]
    [SerializeField] private Transform spawnReference;

    [Header("Distance Presets (meters)")]
    [SerializeField] private float nearDistance = 0.35f;
    [SerializeField] private float mediumDistance = 0.55f;
    [SerializeField] private float farDistance = 0.75f;

    [Header("Random Spawn Area Around Reference")]
    [Tooltip("Horizontal random offset range in meters.")]
    [SerializeField] private float horizontalSpread = 0.18f;

    [Tooltip("Vertical random offset range in meters.")]
    [SerializeField] private float verticalSpread = 0.12f;

    [Header("Size Presets (multipliers)")]
    [SerializeField] private float smallScaleMultiplier = 0.8f;
    [SerializeField] private float mediumScaleMultiplier = 1.0f;
    [SerializeField] private float largeScaleMultiplier = 1.2f;

    private Stage1Config config;
    private Transform objectsParent;
    private OVRSkeleton handSkeleton;

    private List<GameObject> allObjects = new List<GameObject>();
    private Queue<GameObject> pool = new Queue<GameObject>();

    private readonly Dictionary<GameObject, Vector3> originalLocalScales = new Dictionary<GameObject, Vector3>();

    private GameObject currentObject;
    private int cyclesCompleted = 0;
    private bool running = false;

    private bool waitingForTouch = false;
    private bool touchedThisCycle = false;
    private Coroutine cycleCoroutine;

    public void BeginStage(Stage1Config cfg, Transform parent, OVRSkeleton skeleton) {
        config = cfg;
        objectsParent = parent;
        handSkeleton = skeleton;

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

        allObjects = objectsParent.Cast<Transform>().Select(t => t.gameObject).ToList();

        originalLocalScales.Clear();

        foreach (var obj in allObjects) {
            obj.SetActive(false);

            if (!originalLocalScales.ContainsKey(obj))
                originalLocalScales[obj] = obj.transform.localScale;

            var touch = obj.GetComponent<Stage1TouchDisappear>();
            if (touch == null)
                touch = obj.AddComponent<Stage1TouchDisappear>();

            touch.Initialize(handSkeleton, this);
        }

        if (timerUI != null)
            timerUI.Hide();

        pool.Clear();
        FillPool();

        running = true;
        cyclesCompleted = 0;
        currentObject = null;

        FileLogger.Log(
            $"Stage1CycleController: BeginStage | objects={allObjects.Count}, trials={config.trialsAmount}, endless={config.endlessMode}, useTimeLimit={config.useTimeLimit}, timeLimit={config.timeLimitSeconds}, distance={config.targetDistance}, size={config.targetSize}"
        );

        StartNextCycle();
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

        if (currentObject != null)
            currentObject.SetActive(false);

        foreach (var obj in allObjects)
            obj.SetActive(false);

        currentObject = pool.Dequeue();

        PrepareObjectForCurrentSettings(currentObject);
        currentObject.SetActive(true);

        FileLogger.Log(
            $"Stage1CycleController: Starting cycle {cyclesCompleted + 1} with object {currentObject.name}, pos={currentObject.transform.position}, scale={currentObject.transform.localScale}"
        );

        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);

        cycleCoroutine = StartCoroutine(RunCycle());
    }

    private void PrepareObjectForCurrentSettings(GameObject obj) {
        if (obj == null) return;

        obj.transform.position = GetRandomSpawnPosition();
        obj.transform.rotation = Quaternion.identity;

        if (originalLocalScales.TryGetValue(obj, out Vector3 baseScale)) {
            obj.transform.localScale = baseScale * GetScaleMultiplierForCurrentSetting();
        }
    }

    private Vector3 GetRandomSpawnPosition() {
        float distance = GetDistanceForCurrentSetting();

        Vector3 basePos = spawnReference.position + spawnReference.forward * distance;

        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        float randomY = Random.Range(-verticalSpread, verticalSpread);

        Vector3 offset =
            spawnReference.right * randomX +
            spawnReference.up * randomY;

        return basePos + offset;
    }

    private float GetDistanceForCurrentSetting() {
        switch (config.targetDistance) {
            case TargetDistance.Near:
                return nearDistance;
            case TargetDistance.Medium:
                return mediumDistance;
            case TargetDistance.Far:
                return farDistance;
            case TargetDistance.Random:
                return Random.Range(nearDistance, farDistance);
            default:
                return mediumDistance;
        }
    }

    private float GetScaleMultiplierForCurrentSetting() {
        switch (config.targetSize) {
            case TargetSize.Small:
                return smallScaleMultiplier;
            case TargetSize.Medium:
                return mediumScaleMultiplier;
            case TargetSize.Large:
                return largeScaleMultiplier;
            default:
                return mediumScaleMultiplier;
        }
    }

    private IEnumerator RunCycle() {
        waitingForTouch = true;
        touchedThisCycle = false;

        if (!config.useTimeLimit) {
            if (timerUI != null)
                timerUI.Hide();

            yield return new WaitUntil(() => touchedThisCycle || !running);

            if (!running) yield break;

            waitingForTouch = false;
            cyclesCompleted++;
            StartNextCycle();
            yield break;
        }

        float remaining = config.timeLimitSeconds;

        if (timerUI != null)
            timerUI.SetTime(remaining);

        while (remaining > 0f && !touchedThisCycle && running) {
            remaining -= Time.deltaTime;

            if (timerUI != null)
                timerUI.SetTime(Mathf.Max(remaining, 0f));

            yield return null;
        }

        if (!running) yield break;

        waitingForTouch = false;

        if (touchedThisCycle) {
            if (timerUI != null)
                timerUI.Hide();

            cyclesCompleted++;
            StartNextCycle();
            yield break;
        }

        if (timerUI != null)
            timerUI.SetExpiredZero();

        if (currentObject != null)
            currentObject.SetActive(false);

        FileLogger.Log($"Stage1CycleController: Timeout on object {currentObject.name}");

        cyclesCompleted++;

        if (showRedZeroSeconds > 0f)
            yield return new WaitForSeconds(showRedZeroSeconds);

        if (timerUI != null)
            timerUI.Hide();

        if (timeoutPauseSeconds > showRedZeroSeconds)
            yield return new WaitForSeconds(timeoutPauseSeconds - showRedZeroSeconds);

        StartNextCycle();
    }

    public void OnObjectTouched(GameObject obj) {
        if (!running) return;
        if (!waitingForTouch) return;
        if (obj != currentObject) return;

        FileLogger.Log($"Stage1CycleController: Object touched = {obj.name}");

        touchedThisCycle = true;
        obj.SetActive(false);
    }

    private void EndStage() {
        running = false;
        waitingForTouch = false;

        if (cycleCoroutine != null) {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        if (currentObject != null)
            currentObject.SetActive(false);

        if (timerUI != null)
            timerUI.Hide();

        FileLogger.Log("Stage1CycleController: Stage finished.");
    }
}
