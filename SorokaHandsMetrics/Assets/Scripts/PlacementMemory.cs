using UnityEngine;

public static class PlacementMemory {
    public static bool HasPose { get; private set; }
    public static Vector3 TablePos { get; private set; }
    public static Quaternion TableRot { get; private set; }

    public static void SavePose(Vector3 pos, Quaternion rot) {
        HasPose = true;
        TablePos = pos;
        TableRot = rot;

        Debug.Log($"[PlacementMemory] SAVE pos={pos} rot={rot.eulerAngles}");
    }

    public static void Clear() {
        HasPose = false;
        Debug.Log("[PlacementMemory] CLEAR");
    }
}
