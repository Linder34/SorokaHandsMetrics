using UnityEngine;

public class KeepTableLevel : MonoBehaviour {
    void LateUpdate() {
        var e = transform.eulerAngles;
        e.x = 0f;
        e.z = 0f;
        transform.eulerAngles = e;
    }
}
