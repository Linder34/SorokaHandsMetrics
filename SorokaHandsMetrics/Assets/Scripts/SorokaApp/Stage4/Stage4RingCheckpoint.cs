using System.Collections.Generic;
using UnityEngine;

public enum Stage4ArrowForwardAxis {
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public class Stage4RingCheckpoint : MonoBehaviour {

    private Stage4PathController pathController;
    private GameObject currentObject;
    private int checkpointIndex;

    // ============================================================
    // DIRECTIONAL CROSSING
    // ============================================================

    [Header("Directional Crossing")]

    [Tooltip(
        "Small tolerance around the ring plane. " +
        "Prevents jitter from accidentally counting as a crossing.")]
    [SerializeField]
    private float crossingEpsilon = 0.005f;

    /*
     * Becomes true once the object has clearly been
     * detected on the correct BEFORE side of the ring.
     */
    private bool wasOnCorrectStartingSide;

    private bool alreadyPassed;

    /*
     * Some objects can contain more than one collider.
     * We keep track of all colliders currently inside
     * the ring trigger.
     */
    private readonly HashSet<Collider> objectCollidersInside =
        new HashSet<Collider>();


    // ============================================================
    // DIRECTION ARROW
    // ============================================================

    [Header("Direction Arrow")]

    [Tooltip(
        "Drag the DirectionArrow child of this ring here.")]
    [SerializeField]
    private GameObject directionArrow;


    [Tooltip(
        "Which LOCAL direction does the arrow model's TIP " +
        "naturally point toward when its rotation is 0,0,0?")]
    [SerializeField]
    private Stage4ArrowForwardAxis arrowModelForwardAxis =
        Stage4ArrowForwardAxis.NegativeX;


    [Tooltip(
        "Optional additional rotation if the imported arrow " +
        "still needs a small visual adjustment.")]
    [SerializeField]
    private Vector3 arrowExtraRotation = Vector3.zero;


    // ============================================================
    // SOUND
    // ============================================================

    [Header("Success Sound")]

    [Tooltip(
        "Optional existing AudioSource on this ring.")]
    [SerializeField]
    private AudioSource audioSource;


    [Tooltip(
        "Optional explicit clip. If assigned, this is preferred.")]
    [SerializeField]
    private AudioClip successSound;


    [Range(0f, 1f)]
    [SerializeField]
    private float successSoundVolume = 1f;


    // ============================================================
    // INITIALIZE
    // ============================================================

    public void Initialize(
        Stage4PathController controller,
        int index,
        GameObject targetObject) {

        pathController = controller;
        checkpointIndex = index;
        currentObject = targetObject;

        alreadyPassed = false;
        wasOnCorrectStartingSide = false;

        objectCollidersInside.Clear();

        ConfigureDirectionArrow();

        // PathController decides which ring currently needs
        // to display its arrow.
        ShowDirectionArrow(false);
    }


    // ============================================================
    // ARROW
    // ============================================================

    private void ConfigureDirectionArrow() {

        if (directionArrow == null)
            return;

        Transform arrowTransform =
            directionArrow.transform;


        /*
         * The ring's LOCAL +Z direction is the accepted
         * passage direction:
         *
         *     transform.forward
         *
         * We rotate the imported arrow model so its visual
         * tip also points toward the ring's local +Z.
         */
        Vector3 modelForward =
            GetArrowModelForwardVector(
                arrowModelForwardAxis);


        Quaternion correction =
            Quaternion.FromToRotation(
                modelForward,
                Vector3.forward);


        arrowTransform.localRotation =
            correction *
            Quaternion.Euler(
                arrowExtraRotation);
    }


    private Vector3 GetArrowModelForwardVector(
        Stage4ArrowForwardAxis axis) {

        switch (axis) {

            case Stage4ArrowForwardAxis.PositiveX:
                return Vector3.right;

            case Stage4ArrowForwardAxis.NegativeX:
                return Vector3.left;

            case Stage4ArrowForwardAxis.PositiveY:
                return Vector3.up;

            case Stage4ArrowForwardAxis.NegativeY:
                return Vector3.down;

            case Stage4ArrowForwardAxis.PositiveZ:
                return Vector3.forward;

            case Stage4ArrowForwardAxis.NegativeZ:
                return Vector3.back;

            default:
                return Vector3.forward;
        }
    }


    public void ShowDirectionArrow(
        bool show) {

        if (directionArrow != null) {
            directionArrow.SetActive(show);
        }
    }


    // ============================================================
    // TRIGGER ENTER
    // ============================================================

    private void OnTriggerEnter(
        Collider other) {

        if (alreadyPassed)
            return;

        if (pathController == null ||
            currentObject == null ||
            other == null) {

            return;
        }

        if (!BelongsToCurrentObject(other))
            return;


        objectCollidersInside.Add(other);


        float side =
            GetSignedSide(
                currentObject.transform.position);


        /*
         * Negative = BEFORE the ring.
         *
         * This is the correct starting side.
         */
        if (side < -crossingEpsilon) {

            wasOnCorrectStartingSide = true;
        }
    }


    // ============================================================
    // TRIGGER STAY
    // ============================================================

    private void OnTriggerStay(
        Collider other) {

        if (alreadyPassed)
            return;

        if (pathController == null ||
            currentObject == null ||
            other == null) {

            return;
        }

        if (!BelongsToCurrentObject(other))
            return;


        float side =
            GetSignedSide(
                currentObject.transform.position);


        /*
         * If we're still clearly before the ring,
         * remember that we came from the valid side.
         */
        if (side < -crossingEpsilon) {

            wasOnCorrectStartingSide = true;

            return;
        }


        /*
         * VALID CROSSING:
         *
         * We were previously on:
         *
         *       negative side
         *
         * and are now on:
         *
         *       positive side
         *
         * Therefore movement was along:
         *
         *       transform.forward
         */
        if (wasOnCorrectStartingSide &&
            side > crossingEpsilon) {

            TryCompleteCheckpoint();
        }
    }


    // ============================================================
    // TRIGGER EXIT
    // ============================================================

    private void OnTriggerExit(
        Collider other) {

        if (currentObject == null ||
            other == null) {

            return;
        }

        if (!BelongsToCurrentObject(other))
            return;


        objectCollidersInside.Remove(other);


        /*
         * One last crossing check on exit.
         *
         * Useful if the object moves quickly enough
         * to cross between physics updates.
         */
        if (!alreadyPassed &&
            wasOnCorrectStartingSide) {

            float side =
                GetSignedSide(
                    currentObject.transform.position);


            if (side > crossingEpsilon) {

                TryCompleteCheckpoint();

                return;
            }
        }


        /*
         * Object completely left the trigger without
         * completing a valid pass.
         *
         * Reset the crossing attempt.
         */
        if (objectCollidersInside.Count == 0 &&
            !alreadyPassed) {

            wasOnCorrectStartingSide = false;
        }
    }


    // ============================================================
    // COMPLETE RING
    // ============================================================

    private void TryCompleteCheckpoint() {

        if (alreadyPassed)
            return;

        if (pathController == null)
            return;


        /*
         * Returns TRUE only when this is the currently
         * required ring.
         */
        bool accepted =
            pathController.TryPassCheckpoint(
                checkpointIndex);


        /*
         * The object may have crossed this ring in the
         * correct physical direction, but it wasn't the
         * required ring yet.
         */
        if (!accepted) {

            wasOnCorrectStartingSide = false;

            return;
        }


        alreadyPassed = true;
        wasOnCorrectStartingSide = false;

        objectCollidersInside.Clear();


        // --------------------------------------------------------
        // SOUND
        // --------------------------------------------------------

        /*
         * Play the sound from a temporary AudioSource.
         *
         * This allows the sound to continue even though
         * the ring GameObject is immediately disabled.
         */
        if (successSound != null) {

            AudioSource.PlayClipAtPoint(
                successSound,
                transform.position,
                successSoundVolume);
        }
        else if (
            audioSource != null &&
            audioSource.clip != null) {

            AudioSource.PlayClipAtPoint(
                audioSource.clip,
                transform.position,
                audioSource.volume);
        }


        // --------------------------------------------------------
        // DISAPPEAR
        // --------------------------------------------------------

        ShowDirectionArrow(false);

        gameObject.SetActive(false);
    }


    // ============================================================
    // SIDE OF RING
    // ============================================================

    private float GetSignedSide(
        Vector3 worldPosition) {

        Vector3 fromRingToObject =
            worldPosition -
            transform.position;


        /*
         * transform.forward is the accepted direction.
         *
         * Negative:
         *     object is before ring
         *
         * Positive:
         *     object is after ring
         */
        return Vector3.Dot(
            fromRingToObject,
            transform.forward);
    }


    // ============================================================
    // OBJECT CHECK
    // ============================================================

    private bool BelongsToCurrentObject(
        Collider other) {

        if (currentObject == null)
            return false;


        Transform objectTransform =
            currentObject.transform;

        Transform otherTransform =
            other.transform;


        return
            other.gameObject == currentObject ||

            otherTransform.IsChildOf(
                objectTransform) ||

            objectTransform.IsChildOf(
                otherTransform);
    }
}