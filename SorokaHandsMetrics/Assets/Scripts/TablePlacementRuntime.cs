using UnityEngine;
using UnityEngine.SceneManagement;

public class TablePlacementRuntime : MonoBehaviour {
    [Header("Scene to load after saving placement (Setup scene only)")]
    [SerializeField] private string nextSceneName = "MainSceneTest";

    [Header("Optional: assign, otherwise we'll auto-find by name 'Table'")]
    [SerializeField] private Transform table;

    [Header("Optional: will be used to lock in Main scene")]
    [SerializeField] private Behaviour touchHandGrabInteractable;
    [SerializeField] private Behaviour grabbable;
    [SerializeField] private Rigidbody tableRigidbody;

    private void Awake() {
        AutoWire();
        FileLogger.Log($"[TablePlacementRuntime] Awake on scene='{gameObject.scene.name}', table={(table ? table.name : "NULL")}");
    }

    private void AutoWire() {
        if (table == null) {
            foreach (var t in GetComponentsInChildren<Transform>(true)) {
                if (t.name == "Table") {
                    table = t;
                    break;
                }
            }
        }

        if (table == null) {
            FileLogger.Log("[TablePlacementRuntime] AutoWire FAILED – could not find child named 'Table'");
            return;
        }

        if (tableRigidbody == null)
            tableRigidbody = table.GetComponent<Rigidbody>();

        if (touchHandGrabInteractable == null) {
            foreach (var b in table.GetComponents<Behaviour>()) {
                if (b != null && b.GetType().Name.Contains("TouchHandGrab")) {
                    touchHandGrabInteractable = b;
                    break;
                }
            }
        }

        if (grabbable == null) {
            foreach (var b in table.GetComponents<Behaviour>()) {
                if (b != null && b.GetType().Name.Contains("Grabbable")) {
                    grabbable = b;
                    break;
                }
            }
        }
    }

    // SetupScene button calls this (no args -> shows in UnityEvent)
    public void SavePlacementAndGoToNextScene() {
        AutoWire();

        if (table == null) {
            FileLogger.Log("[TablePlacementRuntime] SAVE FAILED – table is NULL");
            return;
        }

        FileLogger.Log($"[TablePlacementRuntime] SAVE called. Table world pos={table.position}");

        PlacementMemory.SavePose(table.position, table.rotation);

        FileLogger.Log($"[TablePlacementRuntime] Loading next scene '{nextSceneName}'");
        SceneManager.LoadScene(nextSceneName);
    }

    // MainScene uses this on startup
    public void LoadPlacementIfAvailable(bool lockAfterLoad = true) {
        AutoWire();

        FileLogger.Log($"[TablePlacementRuntime] LOAD called on scene='{gameObject.scene.name}'");

        if (!PlacementMemory.HasPose) {
            FileLogger.Log("[TablePlacementRuntime] LOAD skipped – no saved pose");
            return;
        }

        if (table == null) {
            FileLogger.Log("[TablePlacementRuntime] LOAD FAILED – table is NULL");
            return;
        }

        FileLogger.Log($"[TablePlacementRuntime] APPLY pos={PlacementMemory.TablePos}");

        table.SetPositionAndRotation(PlacementMemory.TablePos, PlacementMemory.TableRot);
    }
}
