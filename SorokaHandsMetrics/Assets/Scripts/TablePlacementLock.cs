using UnityEngine;

public class TablePlacementLock : MonoBehaviour {
    [Header("Assign the Table (the object you grab)")]
    public Transform table;

    [Header("Disable these when locking (drag from Table)")]
    public Behaviour touchHandGrabInteractable;   // TouchHandGrabInteractable component
    public Behaviour grabbable;                   // your Grabbable component (if it exists)
    public Rigidbody tableRigidbody;              // optional (you already have one)

    private const string Key = "PLACEMENT_TABLE_POSE";

    [System.Serializable]
    private class PoseData {
        public Vector3 pos;
        public Quaternion rot;
    }

    void Awake()
{
    // Auto-find the Table child if not assigned in Inspector/prefab
    if (table == null)
    {
        var t = transform.Find("Table");
        if (t != null) table = t;
    }

    // Auto-find rigidbody if not assigned
    if (table != null && tableRigidbody == null)
        tableRigidbody = table.GetComponent<Rigidbody>();
}


    public void LockAndSave() {
        if (table == null) {
            Debug.LogError("[Placement] Table reference is missing.");
            return;
        }

        // Force perfectly level (no pitch/roll)
        var e = table.eulerAngles;
        e.x = 0f; e.z = 0f;
        table.eulerAngles = e;

        // Disable moving
        if (touchHandGrabInteractable != null) touchHandGrabInteractable.enabled = false;
        if (grabbable != null) grabbable.enabled = false;

        // Keep rigidbody stable (optional)
        if (tableRigidbody != null) {
            tableRigidbody.useGravity = false;
            tableRigidbody.isKinematic = true;
            tableRigidbody.velocity = Vector3.zero;
            tableRigidbody.angularVelocity = Vector3.zero;
        }

        // Save pose
        var data = new PoseData { pos = table.position, rot = table.rotation };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        Debug.Log("[Placement] Locked + saved table pose.");
    }

    public void Unlock() {
        if (touchHandGrabInteractable != null) touchHandGrabInteractable.enabled = true;
        if (grabbable != null) grabbable.enabled = true;

        Debug.Log("[Placement] Unlocked table.");
    }

    public void LoadSavedPoseIfExists() {
        if (table == null) return;
        if (!PlayerPrefs.HasKey(Key)) return;

        var data = JsonUtility.FromJson<PoseData>(PlayerPrefs.GetString(Key));
        table.SetPositionAndRotation(data.pos, data.rot);

        // re-level just in case
        var e = table.eulerAngles;
        e.x = 0f; e.z = 0f;
        table.eulerAngles = e;

        Debug.Log("[Placement] Loaded saved table pose.");
    }

    public void ClearSavedPose() {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        Debug.Log("[Placement] Cleared saved table pose.");
    }
}
