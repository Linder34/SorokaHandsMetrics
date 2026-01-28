using System.Collections.Generic;
using UnityEngine;

public class MenuModeController : MonoBehaviour {
    [Header("UI Roots")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject placementMenuRoot;

    [Header("Placement Selector Menu (shown next to PlacementMenuRoot)")]
    [Tooltip("A separate UI root that contains 2 buttons: Show Objects 1 / Show Objects 2")]
    [SerializeField] private GameObject placementSelectorMenuRoot;

    [Header("Table")]
    [SerializeField] private GameObject tableRoot;
    [SerializeField] private Transform tableTransform;

    [Header("Experiment Object Sets")]
    [Tooltip("Drag the parent 'Objects' here.")]
    [SerializeField] private Transform objects1Root;

    [Tooltip("Drag the parent 'Objects2' here.")]
    [SerializeField] private Transform objects2Root;

    [Tooltip("If true, we treat transforms as LOCAL to each objects root (recommended).")]
    [SerializeField] private bool storeLocalPose = true;

    [Header("Placement Nudge")]
    [SerializeField] private float stepMeters = 0.02f;
    [SerializeField] private bool verticalIsWorldUp = true;
    [SerializeField] private bool moveRelativeToCameraYaw = true;
    [SerializeField] private Transform cameraTransform; // CenterEyeAnchor recommended

    [Header("Experiment Script")]
    [SerializeField] private OVRHandCountdownCycle experiment;

    // ---- internal pose cache ----
    private class ObjState {
        public Transform t;
        public Rigidbody rb;

        public Vector3 localPos;
        public Quaternion localRot;

        public Vector3 worldPos;
        public Quaternion worldRot;

        public bool originalUseGravity;
        public bool originalIsKinematic;
    }

    // Cache per root (Objects / Objects2)
    private readonly Dictionary<Transform, List<ObjState>> _cacheByRoot = new();

    void Awake() {
        if (tableTransform == null && tableRoot != null)
            tableTransform = tableRoot.transform;

        CacheObjectsForRoot(objects1Root);
        CacheObjectsForRoot(objects2Root);

        EnterMainMenu();
    }

    // ----------------- Public UI Button Handlers -----------------

    // Main Menu
    public void OnSettingsPressed() {
        EnterPlacementMode(); // default: show Objects1 only
    }

    public void OnStartTrial1Pressed() {
        StartExperiment(1);
    }

    public void OnStartTrial2Pressed() {
        StartExperiment(2);
    }

    // Placement menu
    public void OnPlacementBackPressed() {
        HideObjects1();
        HideObjects2();
        EnterMainMenu();
    }

    // Placement selector menu (the new two buttons)
    public void OnShowObjects1Pressed() {
        ShowObjects1ForPlacement();
    }

    public void OnShowObjects2Pressed() {
        ShowObjects2ForPlacement();
    }

    // ----------------- Mode Switching -----------------

    public void EnterMainMenu() {
        if (mainMenuRoot) mainMenuRoot.SetActive(true);
        if (placementMenuRoot) placementMenuRoot.SetActive(false);
        if (placementSelectorMenuRoot) placementSelectorMenuRoot.SetActive(false);

        if (tableRoot) tableRoot.SetActive(false);
        // Objects can remain hidden in main menu (recommended)
    }

    private void EnterPlacementMode() {
        if (mainMenuRoot) mainMenuRoot.SetActive(false);
        if (placementMenuRoot) placementMenuRoot.SetActive(true);
        if (placementSelectorMenuRoot) placementSelectorMenuRoot.SetActive(true);

        if (tableRoot) tableRoot.SetActive(true);

        // As requested: when entering Settings/Placement, show ONLY Objects1 by default.
        ShowObjects1ForPlacement();
    }

    // Call this from your "Start Trial 1/2" toggles/buttons
    public void StartExperiment(int whichObjects) {
        // In trial mode: show only the selected set, hide the other
        if (whichObjects == 1) {
            ShowRoot(objects1Root, true);
            ShowRoot(objects2Root, false);
        }
        else if (whichObjects == 2) {
            ShowRoot(objects1Root, false);
            ShowRoot(objects2Root, true);
        }
        else {
            Debug.LogError("[MenuModeController] StartExperiment(whichObjects) must be 1 or 2.");
            return;
        }

        // Reset & enable physics for the set that is active
        var activeRoot = (whichObjects == 1) ? objects1Root : objects2Root;
        ResetObjectsToOrigin(activeRoot);
        SetObjectsPhysics(activeRoot, enableGravity: true, kinematic: false);

        // Hide menus during the experiment
        if (mainMenuRoot) mainMenuRoot.SetActive(false);
        if (placementMenuRoot) placementMenuRoot.SetActive(false);
        if (placementSelectorMenuRoot) placementSelectorMenuRoot.SetActive(false);

        if (tableRoot) tableRoot.SetActive(true);

        // Start experiment logic
        if (experiment != null)
            experiment.StartExperiment(whichObjects);
        else
            Debug.LogError("[MenuModeController] experiment reference not assigned.");
    }

    // ----------------- Placement: Show/Hide object sets -----------------

    private void ShowAllRealObjects(Transform root) {
        if (!root) return;

        foreach (Transform child in root) {
            if (child.name.Contains("Real")) {
                var renderers = child.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers) r.enabled = true;
            }
        }
    }

    private void ShowObjects1ForPlacement() {
        // show objects1, hide objects2
        ShowRoot(objects1Root, true);
        ShowRoot(objects2Root, false);

        ShowAllRealObjects(objects1Root);

        // no gravity in settings mode
        ResetObjectsToOrigin(objects1Root);
        SetObjectsPhysics(objects1Root, enableGravity: false, kinematic: true);
    }

    private void ShowObjects2ForPlacement() {
        // show objects2, hide objects1
        ShowRoot(objects2Root, true);
        ShowRoot(objects1Root, false);

        ShowAllRealObjects(objects2Root);

        // no gravity in settings mode
        ResetObjectsToOrigin(objects2Root);
        SetObjectsPhysics(objects2Root, enableGravity: false, kinematic: true);
    }

    private void HideObjects1() {
        ShowRoot(objects1Root, false);
    }

    private void HideObjects2() {
        ShowRoot(objects2Root, false);
    }

    private void ShowRoot(Transform root, bool show) {
        if (!root) return;
        root.gameObject.SetActive(show);
    }

    // ----------------- Cache / Reset / Physics -----------------

    private void CacheObjectsForRoot(Transform root) {
        if (!root) return;
        if (_cacheByRoot.ContainsKey(root)) return;

        var list = new List<ObjState>();

        // Cache all rigidbodies under the root
        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs) {
            var s = new ObjState
            {
                t = rb.transform,
                rb = rb,
                localPos = rb.transform.localPosition,
                localRot = rb.transform.localRotation,
                worldPos = rb.transform.position,
                worldRot = rb.transform.rotation,
                originalUseGravity = rb.useGravity,
                originalIsKinematic = rb.isKinematic
            };
            list.Add(s);
        }

        _cacheByRoot[root] = list;
    }

    private void SetObjectsPhysics(Transform root, bool enableGravity, bool kinematic) {
        if (!root) return;
        if (!_cacheByRoot.TryGetValue(root, out var list)) return;

        foreach (var o in list) {
            if (o.rb == null) continue;
            o.rb.velocity = Vector3.zero;
            o.rb.angularVelocity = Vector3.zero;
            o.rb.useGravity = enableGravity;
            o.rb.isKinematic = kinematic;
        }
    }

    private void ResetObjectsToOrigin(Transform root) {
        if (!root) return;
        if (!_cacheByRoot.TryGetValue(root, out var list)) return;

        foreach (var o in list) {
            if (!o.t) continue;

            if (o.rb) {
                o.rb.velocity = Vector3.zero;
                o.rb.angularVelocity = Vector3.zero;
            }

            if (storeLocalPose) {
                o.t.localPosition = o.localPos;
                o.t.localRotation = o.localRot;
            }
            else {
                o.t.position = o.worldPos;
                o.t.rotation = o.worldRot;
            }

            if (o.rb) {
                // Keep RB in sync with new pose
                o.rb.position = o.t.position;
                o.rb.rotation = o.t.rotation;
                o.rb.Sleep();
            }
        }
    }

    // ----------------- Placement movement (table nudge) -----------------

    public void MoveUp() => Nudge(Vector3.up);
    public void MoveDown() => Nudge(Vector3.down);
    public void MoveLeft() => Nudge(Vector3.left);
    public void MoveRight() => Nudge(Vector3.right);
    public void MoveFront() => Nudge(Vector3.forward);
    public void MoveBack() => Nudge(Vector3.back);

    private void Nudge(Vector3 dir) {
        if (!tableTransform) {
            Debug.LogError("[MenuModeController] TableTransform is not assigned.");
            return;
        }

        Vector3 moveDir = dir;

        if (dir == Vector3.up || dir == Vector3.down) {
            if (!verticalIsWorldUp)
                moveDir = (dir == Vector3.up) ? tableTransform.up : -tableTransform.up;
        }
        else if (moveRelativeToCameraYaw) {
            if (!cameraTransform) {
                Debug.LogError("[MenuModeController] cameraTransform is required when moveRelativeToCameraYaw is true.");
                return;
            }

            // Use camera yaw only (ignore pitch)
            Vector3 flatForward = cameraTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

            if (dir == Vector3.forward) moveDir = flatForward;
            else if (dir == Vector3.back) moveDir = -flatForward;
            else if (dir == Vector3.right) moveDir = flatRight;
            else if (dir == Vector3.left) moveDir = -flatRight;
        }

        tableTransform.position += moveDir * stepMeters;
    }
}
