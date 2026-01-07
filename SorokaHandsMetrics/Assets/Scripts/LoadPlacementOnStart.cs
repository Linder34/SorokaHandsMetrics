using UnityEngine;

public class LoadPlacementOnStart : MonoBehaviour {
    private TablePlacementLock placement;

    void Awake() {
        placement = GetComponent<TablePlacementLock>();
    }

    void Start() {
        if (placement == null) {
            Debug.LogError("[Placement] TablePlacementLock not found on ExperimentRoot.");
            return;
        }

        // Load saved pose (from SetupSceneTest)
        placement.LoadSavedPoseIfExists();

        // Make sure table is NOT movable in experiment
        if (placement.touchHandGrabInteractable != null)
            placement.touchHandGrabInteractable.enabled = false;

        if (placement.grabbable != null)
            placement.grabbable.enabled = false;

        Debug.Log("[Placement] Table placement loaded and locked.");
    }
}
