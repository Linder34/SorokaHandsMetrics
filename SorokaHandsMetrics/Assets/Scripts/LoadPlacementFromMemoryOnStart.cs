using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LoadPlacementFromMemoryOnStart : MonoBehaviour {
    void Awake() {
        var runtime = GetComponent<TablePlacementRuntime>();
        if (runtime != null)
            runtime.LoadPlacementIfAvailable(lockAfterLoad: true);
    }
}
