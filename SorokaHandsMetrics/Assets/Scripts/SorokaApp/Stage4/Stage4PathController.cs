using System;
using UnityEngine;

public class Stage4PathController : MonoBehaviour {

    // ============================================================
    // CHECKPOINTS
    // ============================================================

    [Header("Checkpoints")]
    [SerializeField]
    private Stage4RingCheckpoint[] checkpoints;


    // ============================================================
    // TABLE
    // ============================================================

    [Header("Table")]

    [Tooltip(
        "Assign the table collider here. " +
        "Used to make sure rings never enter the table.")]
    [SerializeField]
    private Collider tableCollider;


    [Tooltip(
        "Extra clearance between the bottom of a ring " +
        "and the top of the table.")]
    [SerializeField]
    private float ringTableClearance = 0.04f;


    // ============================================================
    // RING SCALE
    // ============================================================

    [Header("Ring Scale")]

    [SerializeField]
    private float largeRingScale = 1.2f;

    [SerializeField]
    private float mediumRingScale = 1.0f;

    [SerializeField]
    private float smallRingScale = 0.8f;


    // ============================================================
    // PATH HEIGHT / AMPLITUDE
    // ============================================================

    [Header("Path Height / Amplitude")]

    [SerializeField]
    private float lowHeight = 0.15f;

    [SerializeField]
    private float mediumHeight = 0.25f;

    [SerializeField]
    private float highHeight = 0.35f;


    // ============================================================
    // NORMAL PATH SPACING
    // ============================================================

    [Header("Normal Path Ring Spacing")]

    [Tooltip(
        "With 3 rings, 0.12 creates approximately " +
        "12%, 50%, 88% positions.")]
    [Range(0.05f, 0.25f)]
    [SerializeField]
    private float normalPathEndpointPadding = 0.12f;


    // ============================================================
    // PATH SHAPE
    // ============================================================

    [Header("Path Shape Tuning")]

    [Tooltip(
        "Amount of sideways movement for curved paths.")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float lateralAmplitudeMultiplier = 0.65f;


    // ============================================================
    // COMPACT ALTERNATING TOWER
    // ============================================================

    [Header("Compact Alternating Tower")]

    [Tooltip(
        "Vertical center-to-center spacing relative to " +
        "the ring diameter/height. " +
        "0.60 keeps the tower significantly shorter.")]
    [Range(0.3f, 1f)]
    [SerializeField]
    private float towerVerticalSpacingMultiplier = 0.60f;


    [Tooltip(
        "How far the middle/alternating rings move sideways, " +
        "relative to the ring diameter.")]
    [Range(0f, 1.5f)]
    [SerializeField]
    private float towerSideOffsetMultiplier = 0.75f;


    // ============================================================
    // STATE
    // ============================================================

    private int currentCheckpointIndex;
    private int wrongCheckpointCount;
    private int activeCheckpointCount;

    private Stage4PathType currentPathType;

    private Stage4PathType previousPathType;
    private bool hasPreviousPathType;


    // ============================================================
    // PUBLIC STATE
    // ============================================================

    public bool PathCompleted =>
        activeCheckpointCount > 0 &&
        currentCheckpointIndex >=
        activeCheckpointCount;


    public int CheckpointsCompleted =>
        currentCheckpointIndex;


    public int WrongCheckpointCount =>
        wrongCheckpointCount;


    public int CheckpointCount =>
        activeCheckpointCount;


    public int NextRequiredRingNumber =>
        PathCompleted ||
        activeCheckpointCount <= 0
            ? 0
            : currentCheckpointIndex + 1;


    public Stage4PathType CurrentPathType =>
        currentPathType;


    // ============================================================
    // SETUP
    // ============================================================

    public void SetupPath(
        GameObject targetObject,
        Vector3 fruitPosition,
        Vector3 basketPosition,
        Stage4Config config) {

        SetupPathInternal(
            targetObject,
            fruitPosition,
            basketPosition,
            config);
    }


    // ============================================================
    // COMPATIBILITY WITH CURRENT CYCLE CONTROLLER
    // ============================================================

    public void SetupPath(
        GameObject targetObject,
        Vector3 fruitPosition,
        Vector3 basketPosition,
        float pathValue,
        Stage4Config config) {

        SetupPathInternal(
            targetObject,
            fruitPosition,
            basketPosition,
            config);
    }


    // ============================================================
    // INTERNAL SETUP
    // ============================================================

    private void SetupPathInternal(
        GameObject targetObject,
        Vector3 fruitPosition,
        Vector3 basketPosition,
        Stage4Config config) {

        gameObject.SetActive(true);

        currentCheckpointIndex = 0;
        wrongCheckpointCount = 0;


        if (checkpoints == null ||
            checkpoints.Length == 0) {

            activeCheckpointCount = 0;

            Debug.LogWarning(
                "Stage4PathController: No checkpoints assigned.");

            return;
        }


        if (config == null) {

            activeCheckpointCount = 0;

            Debug.LogWarning(
                "Stage4PathController: Config is null.");

            return;
        }


        activeCheckpointCount =
            checkpoints.Length;


        // ========================================================
        // RANDOM PATH
        // ========================================================

        ChooseRandomPathType();

        config.pathType =
            currentPathType;


        float pathAmplitude =
            GetPathAmplitude(
                config.pathHeight);


        float ringScale =
            GetRingScale(
                config.ringSize);


        // ========================================================
        // PATH COORDINATE SYSTEM
        // ========================================================

        Vector3 horizontalForward =
            basketPosition -
            fruitPosition;


        horizontalForward.y = 0f;


        if (horizontalForward.sqrMagnitude <
            0.000001f) {

            horizontalForward =
                Vector3.forward;
        }
        else {

            horizontalForward.Normalize();
        }


        /*
         * Left/right relative to the fruit -> basket path.
         */
        Vector3 sideDirection =
            Vector3.Cross(
                Vector3.up,
                horizontalForward);


        if (sideDirection.sqrMagnitude <
            0.000001f) {

            sideDirection =
                Vector3.right;
        }
        else {

            sideDirection.Normalize();
        }


        // ========================================================
        // TABLE HEIGHT
        // ========================================================

        float tableTopY =
            GetTableTopY(
                fruitPosition,
                basketPosition);


        // ========================================================
        // TOWER PREPARATION
        // ========================================================

        Vector3 towerCenter =
            Vector3.Lerp(
                fruitPosition,
                basketPosition,
                0.5f);

        /*
         * Randomly choose whether the stagger goes
         * left or right this cycle.
         */
        float towerSideSign =
            UnityEngine.Random.value < 0.5f
                ? -1f
                : 1f;


        float previousTowerCenterY = 0f;
        float previousTowerRingHeight = 0f;


        // ========================================================
        // CREATE RINGS
        // ========================================================

        for (int i = 0;
             i < checkpoints.Length;
             i++) {

            Stage4RingCheckpoint checkpoint =
                checkpoints[i];


            if (checkpoint == null)
                continue;


            if (i >= activeCheckpointCount) {

                checkpoint.ShowDirectionArrow(false);

                checkpoint.gameObject.SetActive(false);

                continue;
            }


            // ----------------------------------------------------
            // SCALE FIRST
            // ----------------------------------------------------

            checkpoint.transform.localScale =
                Vector3.one *
                ringScale;


            /*
             * Turn on before measuring Renderer bounds.
             */
            checkpoint.gameObject.SetActive(true);

            checkpoint.ShowDirectionArrow(false);


            Vector3 position;
            Vector3 requiredDirection;


            // ====================================================
            // COMPACT ALTERNATING TOWER
            // ====================================================

            if (currentPathType ==
                Stage4PathType.AlternatingTower) {

                // ------------------------------------------------
                // PASS DIRECTION
                // ------------------------------------------------

                /*
                * The tower is a snake.
                *
                * If the staggered middle ring is on the LEFT:
                *
                * Ring 1: right -> left
                * Ring 2: left  -> right
                * Ring 3: right -> left
                *
                * If the staggered ring is on the RIGHT,
                * the entire pattern is mirrored automatically.
                */

                Vector3 directionTowardStaggeredSide =
                    sideDirection * towerSideSign;

                if (i % 2 == 0) {

                    // Ring 1, Ring 3, Ring 5...
                    requiredDirection =
                        directionTowardStaggeredSide;
                }
                else {

                    // Ring 2, Ring 4, Ring 6...
                    requiredDirection =
                        -directionTowardStaggeredSide;
                }


                checkpoint.transform.rotation =
                    Quaternion.LookRotation(
                        requiredDirection.normalized,
                        Vector3.up);


                /*
                 * Temporarily position at tower center so
                 * renderer world bounds are updated.
                 */
                checkpoint.transform.position =
                    towerCenter;


                float currentRingHeight =
                    GetRingWorldHeight(
                        checkpoint);


                /*
                 * Ring height is effectively the diameter
                 * for our circular rings.
                 */
                float ringDiameter =
                    currentRingHeight;


                // ------------------------------------------------
                // VERTICAL POSITION
                // ------------------------------------------------

                float centerY;


                if (i == 0) {

                    /*
                     * Ring 1 bottom is safely above table.
                     */
                    centerY =
                        tableTopY +
                        ringTableClearance +
                        currentRingHeight * 0.5f;
                }

                else {

                    /*
                     * Much smaller vertical spacing than
                     * the previous tower.
                     *
                     * Example:
                     *
                     * ring diameter = 20 cm
                     * multiplier = 0.60
                     *
                     * center difference = 12 cm
                     */
                    float referenceDiameter =
                        Mathf.Max(
                            previousTowerRingHeight,
                            currentRingHeight);


                    float verticalSpacing =
                        referenceDiameter *
                        towerVerticalSpacingMultiplier;


                    centerY =
                        previousTowerCenterY +
                        verticalSpacing;
                }


                // ------------------------------------------------
                // HORIZONTAL STAGGER
                // ------------------------------------------------

                position =
                    towerCenter;


                /*
                 * Ring 1 = centered
                 *
                 * Ring 2 = shifted sideways
                 *
                 * Ring 3 = centered
                 *
                 * Ring 4 = shifted to opposite side
                 *
                 * etc.
                 */
                if (i % 2 == 1) {

                    int staggerNumber =
                        i / 2;


                    float alternatingSide =
                        staggerNumber % 2 == 0
                            ? towerSideSign
                            : -towerSideSign;


                    float sideOffset =
                        ringDiameter *
                        towerSideOffsetMultiplier;


                    position +=
                        sideDirection *
                        sideOffset *
                        alternatingSide;
                }


                position.y =
                    centerY;


                previousTowerCenterY =
                    centerY;


                previousTowerRingHeight =
                    currentRingHeight;
            }


            // ====================================================
            // NORMAL PATH TYPES
            // ====================================================

            else {

                float t =
                    GetNormalRingT(i);


                position =
                    GetPathPosition(
                        fruitPosition,
                        basketPosition,
                        t,
                        pathAmplitude,
                        sideDirection,
                        currentPathType);


                requiredDirection =
                    GetPathDirection(
                        fruitPosition,
                        basketPosition,
                        t,
                        pathAmplitude,
                        sideDirection,
                        currentPathType);


                if (requiredDirection.sqrMagnitude >
                    0.000001f) {

                    checkpoint.transform.rotation =
                        Quaternion.LookRotation(
                            requiredDirection.normalized,
                            Vector3.up);
                }


                // ------------------------------------------------
                // APPLY TEMPORARY POSITION
                // ------------------------------------------------

                checkpoint.transform.position =
                    position;


                // ------------------------------------------------
                // MAKE SURE RING CANNOT ENTER TABLE
                // ------------------------------------------------

                float ringHeight =
                    GetRingWorldHeight(
                        checkpoint);


                float minimumCenterY =
                    tableTopY +
                    ringTableClearance +
                    ringHeight * 0.5f;


                if (position.y <
                    minimumCenterY) {

                    position.y =
                        minimumCenterY;
                }
            }


            // ====================================================
            // FINAL POSITION
            // ====================================================

            checkpoint.transform.position =
                position;


            // ====================================================
            // INITIALIZE
            // ====================================================

            checkpoint.Initialize(
                this,
                i,
                targetObject);


            checkpoint.gameObject.SetActive(
                true);
        }


        UpdateDirectionArrows();


        Debug.Log(
            $"Stage4 path created. " +
            $"Type={currentPathType}, " +
            $"Rings={activeCheckpointCount}, " +
            $"RingSize={config.ringSize}, " +
            $"PathHeight={config.pathHeight}, " +
            $"TableTop={tableTopY:F3}");
    }


    // ============================================================
    // TABLE TOP
    // ============================================================

    private float GetTableTopY(
        Vector3 fruitPosition,
        Vector3 basketPosition) {

        if (tableCollider != null) {

            return
                tableCollider.bounds.max.y;
        }


        /*
         * Fallback only.
         *
         * The actual Table Collider is much better.
         */
        Debug.LogWarning(
            "Stage4PathController: Table Collider is not assigned. " +
            "Using estimated table height.");


        return
            Mathf.Min(
                fruitPosition.y,
                basketPosition.y) -
            0.02f;
    }


    // ============================================================
    // NORMAL PATH RING DISTRIBUTION
    // ============================================================

    private float GetNormalRingT(
        int ringIndex) {

        if (activeCheckpointCount <= 1)
            return 0.5f;


        float progress =
            ringIndex /
            (activeCheckpointCount - 1f);


        return Mathf.Lerp(
            normalPathEndpointPadding,
            1f - normalPathEndpointPadding,
            progress);
    }


    // ============================================================
    // RANDOM PATH
    // ============================================================

    private void ChooseRandomPathType() {

        Stage4PathType[] allTypes =
            (Stage4PathType[])
            Enum.GetValues(
                typeof(Stage4PathType));


        if (allTypes == null ||
            allTypes.Length == 0) {

            currentPathType =
                Stage4PathType.Arch;

            return;
        }


        Stage4PathType selected =
            allTypes[
                UnityEngine.Random.Range(
                    0,
                    allTypes.Length)];


        /*
         * Avoid same type twice in a row.
         */
        if (hasPreviousPathType &&
            allTypes.Length > 1 &&
            selected == previousPathType) {

            int index =
                Array.IndexOf(
                    allTypes,
                    selected);


            selected =
                allTypes[
                    (index + 1) %
                    allTypes.Length];
        }


        currentPathType =
            selected;


        previousPathType =
            selected;


        hasPreviousPathType =
            true;
    }


    // ============================================================
    // PATH POSITION
    // ============================================================

    private Vector3 GetPathPosition(
        Vector3 fruitPosition,
        Vector3 basketPosition,
        float t,
        float amplitude,
        Vector3 sideDirection,
        Stage4PathType pathType) {

        Vector3 position =
            Vector3.Lerp(
                fruitPosition,
                basketPosition,
                t);


        float lateralAmplitude =
            amplitude *
            lateralAmplitudeMultiplier;


        switch (pathType) {

            // ====================================================
            // STRAIGHT
            // ====================================================

            case Stage4PathType.Straight:

                return position;


            // ====================================================
            // ARCH
            // ====================================================

            case Stage4PathType.Arch: {

                    float height =
                        Mathf.Sin(
                            t *
                            Mathf.PI) *
                        amplitude;


                    position +=
                        Vector3.up *
                        height;


                    return position;
                }


            // ====================================================
            // ZIGZAG
            // ====================================================

            case Stage4PathType.ZigZag: {

                    float sideOffset =
                        Mathf.Sin(
                            t *
                            Mathf.PI *
                            3f) *
                        lateralAmplitude;


                    position +=
                        sideDirection *
                        sideOffset;


                    return position;
                }


            // ====================================================
            // SIDE CURVE
            // ====================================================

            case Stage4PathType.SideCurve: {

                    float sideOffset =
                        Mathf.Sin(
                            t *
                            Mathf.PI) *
                        lateralAmplitude;


                    position +=
                        sideDirection *
                        sideOffset;


                    return position;
                }


            // ====================================================
            // S CURVE
            // ====================================================

            case Stage4PathType.SCurve: {

                    float sideOffset =
                        Mathf.Sin(
                            t *
                            Mathf.PI *
                            2f) *
                        lateralAmplitude;


                    position +=
                        sideDirection *
                        sideOffset;


                    return position;
                }


            default:

                return position;
        }
    }


    // ============================================================
    // PATH DIRECTION
    // ============================================================

    private Vector3 GetPathDirection(
        Vector3 fruitPosition,
        Vector3 basketPosition,
        float t,
        float amplitude,
        Vector3 sideDirection,
        Stage4PathType pathType) {

        const float sampleAmount =
            0.01f;


        float previousT =
            Mathf.Clamp01(
                t - sampleAmount);


        float nextT =
            Mathf.Clamp01(
                t + sampleAmount);


        Vector3 previousPosition =
            GetPathPosition(
                fruitPosition,
                basketPosition,
                previousT,
                amplitude,
                sideDirection,
                pathType);


        Vector3 nextPosition =
            GetPathPosition(
                fruitPosition,
                basketPosition,
                nextT,
                amplitude,
                sideDirection,
                pathType);


        Vector3 direction =
            nextPosition -
            previousPosition;


        if (direction.sqrMagnitude <
            0.000001f) {

            direction =
                basketPosition -
                fruitPosition;
        }


        if (direction.sqrMagnitude <
            0.000001f) {

            direction =
                Vector3.forward;
        }


        return
            direction.normalized;
    }


    // ============================================================
    // ACTUAL RING VISUAL HEIGHT
    // ============================================================

    private float GetRingWorldHeight(
        Stage4RingCheckpoint checkpoint) {

        if (checkpoint == null)
            return 0.15f;


        Renderer[] renderers =
            checkpoint.GetComponentsInChildren<Renderer>(
                true);


        bool foundRenderer =
            false;


        Bounds combinedBounds =
            new Bounds();


        foreach (Renderer renderer
                 in renderers) {

            if (renderer == null)
                continue;


            /*
             * Ignore arrow visual when measuring ring.
             */
            if (renderer.GetComponentInParent<
                    Stage4DirectionArrowAnimator>() != null) {

                continue;
            }


            if (!foundRenderer) {

                combinedBounds =
                    renderer.bounds;

                foundRenderer =
                    true;
            }

            else {

                combinedBounds.Encapsulate(
                    renderer.bounds);
            }
        }


        if (foundRenderer &&
            combinedBounds.size.y >
            0.001f) {

            return
                combinedBounds.size.y;
        }


        // ========================================================
        // FALLBACK TO COLLIDER
        // ========================================================

        Collider collider =
            checkpoint.GetComponent<Collider>();


        if (collider != null &&
            collider.bounds.size.y >
            0.001f) {

            return
                collider.bounds.size.y;
        }


        return 0.15f;
    }


    // ============================================================
    // ARROWS
    // ============================================================

    private void UpdateDirectionArrows() {

        if (checkpoints == null)
            return;


        for (int i = 0;
             i < checkpoints.Length;
             i++) {

            Stage4RingCheckpoint checkpoint =
                checkpoints[i];


            if (checkpoint == null)
                continue;


            bool shouldShow =
                !PathCompleted &&
                i == currentCheckpointIndex;


            checkpoint.ShowDirectionArrow(
                shouldShow);
        }
    }


    // ============================================================
    // PASS CHECKPOINT
    // ============================================================

    public bool TryPassCheckpoint(
        int checkpointIndex) {

        if (PathCompleted)
            return false;


        // ========================================================
        // CORRECT RING
        // ========================================================

        if (checkpointIndex ==
            currentCheckpointIndex) {

            Debug.Log(
                $"Stage4: Ring " +
                $"{checkpointIndex + 1} completed.");


            currentCheckpointIndex++;


            UpdateDirectionArrows();


            if (PathCompleted) {

                Debug.Log(
                    "Stage4: PATH COMPLETED!");
            }


            return true;
        }


        // ========================================================
        // ALREADY COMPLETED RING
        // ========================================================

        if (checkpointIndex <
            currentCheckpointIndex) {

            return false;
        }


        // ========================================================
        // WRONG FUTURE RING
        // ========================================================

        wrongCheckpointCount++;


        Debug.Log(
            $"Stage4: Wrong ring. " +
            $"Expected Ring " +
            $"{currentCheckpointIndex + 1}, " +
            $"entered Ring " +
            $"{checkpointIndex + 1}.");


        return false;
    }


    // ============================================================
    // LOGGER API
    // ============================================================

    public bool TryGetCurrentCheckpointPosition(
        out Vector3 position) {

        position =
            Vector3.zero;


        if (checkpoints == null)
            return false;


        if (activeCheckpointCount <= 0)
            return false;


        if (PathCompleted)
            return false;


        if (currentCheckpointIndex < 0 ||
            currentCheckpointIndex >=
            activeCheckpointCount) {

            return false;
        }


        Stage4RingCheckpoint checkpoint =
            checkpoints[
                currentCheckpointIndex];


        if (checkpoint == null)
            return false;


        position =
            checkpoint.transform.position;


        return true;
    }


    public Vector3 GetCheckpointPosition(
        int checkpointIndex) {

        if (checkpoints == null)
            return Vector3.zero;


        if (checkpointIndex < 0 ||
            checkpointIndex >=
            activeCheckpointCount) {

            return Vector3.zero;
        }


        Stage4RingCheckpoint checkpoint =
            checkpoints[
                checkpointIndex];


        if (checkpoint == null)
            return Vector3.zero;


        return
            checkpoint.transform.position;
    }


    // ============================================================
    // RESET
    // ============================================================

    public void ResetPath() {

        currentCheckpointIndex = 0;
        wrongCheckpointCount = 0;


        /*
         * Keep activeCheckpointCount because logger
         * may still need it while closing the cycle.
         */

        if (checkpoints == null)
            return;


        foreach (
            Stage4RingCheckpoint checkpoint
            in checkpoints) {

            if (checkpoint == null)
                continue;


            checkpoint.ShowDirectionArrow(
                false);


            checkpoint.gameObject.SetActive(
                false);
        }
    }


    // ============================================================
    // HIDE
    // ============================================================

    public void HidePath() {

        ResetPath();

        gameObject.SetActive(false);
    }


    // ============================================================
    // RING SCALE
    // ============================================================

    private float GetRingScale(
        Stage4RingSize size) {

        switch (size) {

            case Stage4RingSize.Large:
                return largeRingScale;


            case Stage4RingSize.Medium:
                return mediumRingScale;


            case Stage4RingSize.Small:
                return smallRingScale;


            default:
                return mediumRingScale;
        }
    }


    // ============================================================
    // PATH AMPLITUDE
    // ============================================================

    private float GetPathAmplitude(
        Stage4PathHeight height) {

        switch (height) {

            case Stage4PathHeight.Low:
                return lowHeight;


            case Stage4PathHeight.Medium:
                return mediumHeight;


            case Stage4PathHeight.High:
                return highHeight;


            default:
                return mediumHeight;
        }
    }
}