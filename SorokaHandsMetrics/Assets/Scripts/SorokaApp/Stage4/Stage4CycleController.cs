using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

[DefaultExecutionOrder(10000)]
public class Stage4CycleController : MonoBehaviour {
    [Header("UI / Flow")]
    [SerializeField] private Stage4FloatingTimerUI timerUI;
    [SerializeField] private RehabMenuController rehabMenuController;
    [SerializeField] private Stage4ScoreUI scoreUI;

    [Header("Data Logging")]
    [SerializeField] private Stage4HandDataLogger dataLogger;

    [Header("Grab Detection")]
    [SerializeField] private TouchHandGrabInteractor rightHandInteractor;
    [SerializeField] private TouchHandGrabInteractor leftHandInteractor;

    [Header("Stage Objects")]
    [SerializeField] private GameObject tableObject;
    [SerializeField] private Collider tableCollider;

    [Header("Placement")]
    [SerializeField] private Transform placementTarget;
    [SerializeField] private Stage4PlacementZone placementZone;

    [Header("Path")]
    [SerializeField] private Stage4PathController pathController;

    [Header("Table Random Area")]
    [SerializeField] private float tableHalfWidth = 0.35f;
    [SerializeField] private float tableHalfDepth = 0.25f;
    [SerializeField] private float objectHeightOffset = 0.06f;
    [SerializeField] private float basketHeightOffset = 0.02f;
    [SerializeField] private float minimumObjectBasketDistance = 0.25f;
    [SerializeField] private int randomPositionAttempts = 30;

    [Header("Pause Settings")]
    [SerializeField] private float timeoutPauseSeconds = 2f;
    [SerializeField] private float successPauseSeconds = 2f;
    [SerializeField] private float showRedZeroSeconds = 0.2f;

    [Header("Spawn Reference")]
    [SerializeField] private Transform spawnReference;

    [Header("Fruit Size Presets")]
    [SerializeField] private float smallScaleMultiplier = 0.8f;
    [SerializeField] private float mediumScaleMultiplier = 1f;
    [SerializeField] private float largeScaleMultiplier = 1.2f;

    [Header("Basket Size Presets")]
    [SerializeField] private float basketSmallScaleMultiplier = 0.8f;
    [SerializeField] private float basketMediumScaleMultiplier = 1f;
    [SerializeField] private float basketLargeScaleMultiplier = 1.2f;

    [SerializeField] private GameObject backToMenuButton;

    private Stage4Config config;
    private Transform objectsParent;
    private OVRSkeleton handSkeleton;

    private List<GameObject> allObjects = new List<GameObject>();
    private Queue<GameObject> pool = new Queue<GameObject>();
    private readonly Dictionary<GameObject, Vector3> originalLocalScales = new Dictionary<GameObject, Vector3>();

    private Vector3 originalBasketScale;

    private GameObject currentObject;
    private int cyclesCompleted;
    private int score;

    private bool running;
    private bool waitingForPlacement;
    private bool cycleSucceeded;
    private bool objectWasGrabbed;

    private float cycleStartTime;
    private float currentContinuousTouchSeconds;
    private float maxContinuousTouchSeconds;

    private Coroutine cycleCoroutine;

    // Meta Interaction can restore an interactable to its authored scale while it is selected.
    // Keep the Stage 3 size preset authoritative for the full grab/placement cycle.
    private Vector3 desiredCurrentObjectLocalScale = Vector3.one;
    private bool hasDesiredCurrentObjectScale;

    private void OnEnable() {
        Application.onBeforeRender += EnforceCurrentObjectScale;
    }

    private void OnDisable() {
        Application.onBeforeRender -= EnforceCurrentObjectScale;
    }

    private void LateUpdate() {
        EnforceCurrentObjectScale();
    }

    private void EnforceCurrentObjectScale() {
        if (!running || !waitingForPlacement || !hasDesiredCurrentObjectScale)
            return;

        if (currentObject == null || !currentObject.activeInHierarchy)
            return;

        Transform objectTransform = currentObject.transform;

        // Avoid writing to the Transform every frame unless another component changed it.
        if ((objectTransform.localScale - desiredCurrentObjectLocalScale).sqrMagnitude > 0.0000001f)
            objectTransform.localScale = desiredCurrentObjectLocalScale;
    }

    private void Awake() {
        if (scoreUI != null)
            scoreUI.gameObject.SetActive(false);

        if (backToMenuButton != null)
            backToMenuButton.SetActive(false);

        if (placementTarget != null)
            placementTarget.gameObject.SetActive(false);

        if (tableObject != null)
            tableObject.SetActive(false);

        if (pathController != null)
            pathController.HidePath();
    }

    public void BeginStage(Stage4Config cfg, Transform parent, OVRSkeleton skeleton) {
        config = cfg;
        objectsParent = parent;
        handSkeleton = skeleton;

        if (scoreUI != null)
            scoreUI.gameObject.SetActive(true);

        if (objectsParent == null || handSkeleton == null || spawnReference == null || placementTarget == null || placementZone == null || pathController == null) {
            FileLogger.Log("Stage4CycleController: Missing required references.");
            return;
        }

        if (tableObject != null)
            tableObject.SetActive(true);

        objectsParent.gameObject.SetActive(true);
        placementTarget.gameObject.SetActive(true);

        originalBasketScale = placementTarget.localScale;

        allObjects = objectsParent.Cast<Transform>().Select(t => t.gameObject).ToList();

        foreach (var obj in allObjects) {
            obj.SetActive(false);

            if (!originalLocalScales.ContainsKey(obj))
                originalLocalScales[obj] = obj.transform.localScale;
        }

        if (timerUI != null)
            timerUI.Hide();

        if (dataLogger != null)
            dataLogger.BeginSession(config, handSkeleton);

        pool.Clear();
        FillPool();

        running = true;
        waitingForPlacement = false;
        cyclesCompleted = 0;
        score = 0;
        currentObject = null;

        if (backToMenuButton != null)
            backToMenuButton.SetActive(config.endlessMode);

        UpdateScoreUI();
        StartNextCycle();
    }

    public void BackToMenuFromEndlessMode() {
        EndStage();
    }

    private void FillPool() {
        var shuffled = allObjects.OrderBy(_ => Random.value).ToList();

        foreach (var obj in shuffled)
            pool.Enqueue(obj);
    }

    private void StartNextCycle() {
        if (!running) return;

        waitingForPlacement = false;
        hasDesiredCurrentObjectScale = false;

        if (!config.endlessMode && cyclesCompleted >= config.trialsAmount) {
            EndStage();
            return;
        }

        if (pool.Count == 0)
            FillPool();

        foreach (var obj in allObjects)
            obj.SetActive(false);

        currentObject = pool.Dequeue();

        Vector3 objectPos;
        Vector3 basketPos;
        GetRandomObjectAndBasketPositions(out objectPos, out basketPos);

        PrepareObject(currentObject, objectPos);

        placementTarget.gameObject.SetActive(true);

        placementTarget.position = basketPos;
        placementTarget.localScale =
            originalBasketScale * GetBasketScaleMultiplierForCurrentSetting();

        placementZone.SetCurrentObject(currentObject);

        if (pathController != null) {
            float tableTopY =
                tableCollider != null
                    ? tableCollider.bounds.max.y
                    : Mathf.Max(objectPos.y, basketPos.y);

            pathController.SetupPath(
                currentObject,
                objectPos,
                basketPos,
                tableTopY,
                config);
        }

        cycleSucceeded = false;
        objectWasGrabbed = false;
        currentContinuousTouchSeconds = 0f;
        maxContinuousTouchSeconds = 0f;
        cycleStartTime = Time.time;

        currentObject.SetActive(true);

        if (dataLogger != null)
            dataLogger.BeginCycle(cyclesCompleted, currentObject);

        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);

        cycleCoroutine = StartCoroutine(RunPlacementCycle());
    }

    private void PrepareObject(GameObject obj, Vector3 position) {
        obj.transform.position = position;

        if (originalLocalScales.TryGetValue(obj, out Vector3 baseScale))
            desiredCurrentObjectLocalScale = baseScale * GetScaleMultiplierForCurrentSetting();
        else
            desiredCurrentObjectLocalScale = obj.transform.localScale;

        obj.transform.localScale = desiredCurrentObjectLocalScale;
        hasDesiredCurrentObjectScale = true;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private float HorizontalDistance(
    Vector3 a,
    Vector3 b) {

        Vector2 a2 =
            new Vector2(
                a.x,
                a.z);

        Vector2 b2 =
            new Vector2(
                b.x,
                b.z);

        return Vector2.Distance(
            a2,
            b2);
    }

    private void GetRandomObjectAndBasketPositions(
    out Vector3 objectPos,
    out Vector3 basketPos) {

        if (tableCollider == null) {
            objectPos =
                spawnReference != null
                    ? spawnReference.position
                    : Vector3.zero;

            basketPos = objectPos;

            return;
        }

        Bounds bounds =
            tableCollider.bounds;

        const float margin = 0.05f;


        float minX =
            bounds.min.x + margin;

        float maxX =
            bounds.max.x - margin;

        float minZ =
            bounds.min.z + margin;

        float maxZ =
            bounds.max.z - margin;


        float usableWidth =
            maxX - minX;

        float usableDepth =
            maxZ - minZ;


        /*
         * Maximum possible horizontal distance
         * available on this table.
         */
        float maxPossibleDistance =
            Mathf.Sqrt(
                usableWidth * usableWidth +
                usableDepth * usableDepth);


        /*
         * Don't ask the randomizer to achieve
         * something physically impossible.
         */
        float effectiveMinimumDistance =
            Mathf.Min(
                minimumObjectBasketDistance,
                maxPossibleDistance * 0.95f);


        if (minimumObjectBasketDistance >
            maxPossibleDistance) {

            Debug.LogWarning(
                $"Stage4: Requested minimum object/basket " +
                $"distance {minimumObjectBasketDistance:F2}m " +
                $"is larger than the usable table diagonal " +
                $"{maxPossibleDistance:F2}m. " +
                $"Using {effectiveMinimumDistance:F2}m instead.");
        }


        /*
         * Spawn on opposite sides along the
         * longest dimension of the table.
         */
        bool splitAlongX =
            usableWidth >= usableDepth;


        bool objectOnNegativeSide =
            Random.value < 0.5f;


        float objectY =
            bounds.max.y +
            objectHeightOffset;

        float basketY =
            bounds.max.y +
            basketHeightOffset;


        /*
         * Only use the outer 35% of each side.
         *
         * This keeps the fruit and basket
         * naturally separated.
         */
        const float sideFraction = 0.35f;


        for (int attempt = 0;
             attempt < randomPositionAttempts;
             attempt++) {

            float objectX;
            float objectZ;

            float basketX;
            float basketZ;


            if (splitAlongX) {

                float leftMax =
                    Mathf.Lerp(
                        minX,
                        maxX,
                        sideFraction);

                float rightMin =
                    Mathf.Lerp(
                        minX,
                        maxX,
                        1f - sideFraction);


                if (objectOnNegativeSide) {

                    objectX =
                        Random.Range(
                            minX,
                            leftMax);

                    basketX =
                        Random.Range(
                            rightMin,
                            maxX);
                }
                else {

                    objectX =
                        Random.Range(
                            rightMin,
                            maxX);

                    basketX =
                        Random.Range(
                            minX,
                            leftMax);
                }


                objectZ =
                    Random.Range(
                        minZ,
                        maxZ);

                basketZ =
                    Random.Range(
                        minZ,
                        maxZ);
            }

            else {

                float bottomMax =
                    Mathf.Lerp(
                        minZ,
                        maxZ,
                        sideFraction);

                float topMin =
                    Mathf.Lerp(
                        minZ,
                        maxZ,
                        1f - sideFraction);


                if (objectOnNegativeSide) {

                    objectZ =
                        Random.Range(
                            minZ,
                            bottomMax);

                    basketZ =
                        Random.Range(
                            topMin,
                            maxZ);
                }
                else {

                    objectZ =
                        Random.Range(
                            topMin,
                            maxZ);

                    basketZ =
                        Random.Range(
                            minZ,
                            bottomMax);
                }


                objectX =
                    Random.Range(
                        minX,
                        maxX);

                basketX =
                    Random.Range(
                        minX,
                        maxX);
            }


            objectPos =
                new Vector3(
                    objectX,
                    objectY,
                    objectZ);

            basketPos =
                new Vector3(
                    basketX,
                    basketY,
                    basketZ);


            if (HorizontalDistance(
                    objectPos,
                    basketPos) >=
                effectiveMinimumDistance) {

                return;
            }
        }


        // ============================================================
        // GUARANTEED FALLBACK
        // ============================================================

        /*
         * If random generation somehow couldn't find
         * a suitable pair, use opposite corners.
         */

        bool flipCorner =
            Random.value < 0.5f;


        if (!flipCorner) {

            objectPos =
                new Vector3(
                    minX,
                    objectY,
                    minZ);

            basketPos =
                new Vector3(
                    maxX,
                    basketY,
                    maxZ);
        }
        else {

            objectPos =
                new Vector3(
                    minX,
                    objectY,
                    maxZ);

            basketPos =
                new Vector3(
                    maxX,
                    basketY,
                    minZ);
        }
    }

    private Vector3 GetRandomTablePosition(float heightOffset) {
        if (tableCollider == null)
            return spawnReference.position;

        Bounds bounds = tableCollider.bounds;

        float margin = 0.05f;

        float x = Random.Range(
            bounds.min.x + margin,
            bounds.max.x - margin);

        float z = Random.Range(
            bounds.min.z + margin,
            bounds.max.z - margin);

        float y = bounds.max.y + heightOffset;

        return new Vector3(x, y, z);
    }

    private float GetScaleMultiplierForCurrentSetting() {
        switch (config.targetSize) {
            case TargetSize.Small: return smallScaleMultiplier;
            case TargetSize.Medium: return mediumScaleMultiplier;
            case TargetSize.Large: return largeScaleMultiplier;
            default: return mediumScaleMultiplier;
        }
    }

    private float GetBasketScaleMultiplierForCurrentSetting() {
        switch (config.targetSize) {
            case TargetSize.Small: return basketSmallScaleMultiplier;
            case TargetSize.Medium: return basketMediumScaleMultiplier;
            case TargetSize.Large: return basketLargeScaleMultiplier;
            default: return basketMediumScaleMultiplier;
        }
    }

    private IEnumerator RunPlacementCycle() {
        waitingForPlacement = true;

        float remaining = config.timeLimitSeconds;

        if (config.useTimeLimit && timerUI != null)
            timerUI.SetTime(remaining);
        else if (timerUI != null)
            timerUI.Hide();

        while (running) {
            bool isGrabbed = IsCurrentObjectGrabbed();

            if (isGrabbed) {
                objectWasGrabbed = true;
                currentContinuousTouchSeconds += Time.deltaTime;
                maxContinuousTouchSeconds = Mathf.Max(maxContinuousTouchSeconds, currentContinuousTouchSeconds);
            }
            else {
                currentContinuousTouchSeconds = 0f;

                if (objectWasGrabbed &&
                    pathController != null &&
                    pathController.PathCompleted &&
                    placementZone.ObjectInside)
                {
                    cycleSucceeded = true;
                    break;
                }
            }

            if (config.useTimeLimit) {
                remaining -= Time.deltaTime;

                if (timerUI != null)
                    timerUI.SetTime(Mathf.Max(remaining, 0f));

                if (remaining <= 0f) {
                    yield return TimeoutCycle();
                    yield break;
                }
            }

            SampleData();
            yield return null;
        }

        waitingForPlacement = false;

        if (!running)
            yield break;

        if (cycleSucceeded) {
            CompleteCycleSuccess();
            yield return new WaitForSeconds(successPauseSeconds);
            StartNextCycle();
        }
    }

    private bool IsCurrentObjectGrabbed() {
        if (!waitingForPlacement || currentObject == null)
            return false;

        return IsSelectedHandGrabbingCurrentObject();
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

        return selectedTransform == currentTransform ||
               selectedTransform.IsChildOf(currentTransform) ||
               currentTransform.IsChildOf(selectedTransform);
    }

    private IEnumerator TimeoutCycle() {
        cycleSucceeded = false;
        waitingForPlacement = false;
        hasDesiredCurrentObjectScale = false;

        float totalTime = Time.time - cycleStartTime;

        if (timerUI != null)
            timerUI.SetExpiredZero();

        if (dataLogger != null)
            dataLogger.EndCycle(
                "Timeout",
                totalTime,
                maxContinuousTouchSeconds);

        if (pathController != null)
            pathController.ResetPath();

        if (currentObject != null)
            currentObject.SetActive(false);

        cyclesCompleted++;
        UpdateScoreUI();

        if (showRedZeroSeconds > 0f)
            yield return new WaitForSeconds(showRedZeroSeconds);

        if (timerUI != null)
            timerUI.Hide();

        if (timeoutPauseSeconds > showRedZeroSeconds)
            yield return new WaitForSeconds(timeoutPauseSeconds - showRedZeroSeconds);

        StartNextCycle();
    }

    private void CompleteCycleSuccess() {
        hasDesiredCurrentObjectScale = false;

        float totalTime = Time.time - cycleStartTime;

        if (timerUI != null)
            timerUI.Hide();

        if (dataLogger != null)
            dataLogger.EndCycle("Success", totalTime, maxContinuousTouchSeconds);

        cyclesCompleted++;
        score++;

        UpdateScoreUI();

        if (pathController != null)
            pathController.ResetPath();

        var effect = currentObject != null ? currentObject.GetComponent<Stage4TouchDisappear>() : null;

        if (effect != null)
            effect.PlaySuccessEffect();
        else if (currentObject != null)
            currentObject.SetActive(false);
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

    private void SampleData() {
        if (dataLogger == null)
            return;

        dataLogger.SampleTrajectory(IsCurrentObjectGrabbed(), currentContinuousTouchSeconds);
    }

    public void SetCurrentObjectTouchState(GameObject obj, bool isTouching) {
        // Not used in Stage 3.
    }

    public void EndStage() {
        running = false;
        waitingForPlacement = false;
        hasDesiredCurrentObjectScale = false;

        if (cycleCoroutine != null) {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        if (dataLogger != null)
            dataLogger.EndSessionScore(score, cyclesCompleted);

        if (timerUI != null)
            timerUI.Hide();

        if (backToMenuButton != null)
            backToMenuButton.SetActive(false);

        if (scoreUI != null)
            scoreUI.gameObject.SetActive(false);

        if (currentObject != null)
            currentObject.SetActive(false);

        if (pathController != null)
            pathController.HidePath();

        foreach (var obj in allObjects) {
            if (obj == null)
                continue;

            if (originalLocalScales.TryGetValue(obj, out Vector3 baseScale))
                obj.transform.localScale = baseScale;

            obj.SetActive(false);
        }

        if (objectsParent != null)
            objectsParent.gameObject.SetActive(false);

        if (placementZone != null)
            placementZone.ResetZone();

        if (placementTarget != null) {
            placementTarget.localScale = originalBasketScale;
            placementTarget.gameObject.SetActive(false);
        }

        if (tableObject != null)
            tableObject.SetActive(false);

        if (rehabMenuController != null)
            rehabMenuController.EnterStage4Settings();
    }
}