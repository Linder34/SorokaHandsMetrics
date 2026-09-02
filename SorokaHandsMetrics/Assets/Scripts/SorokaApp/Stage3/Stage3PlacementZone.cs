using UnityEngine;

public class Stage3PlacementZone : MonoBehaviour {
    private GameObject currentObject;
    private bool objectInside;

    public bool ObjectInside => objectInside;

    public void SetCurrentObject(GameObject obj) {
        currentObject = obj;
        objectInside = false;
    }

    public void ResetZone() {
        currentObject = null;
        objectInside = false;
    }

    private void OnTriggerEnter(Collider other) {
        if (IsCurrentObjectCollider(other))
            objectInside = true;
    }

    private void OnTriggerStay(Collider other) {
        if (IsCurrentObjectCollider(other))
            objectInside = true;
    }

    private void OnTriggerExit(Collider other) {
        if (IsCurrentObjectCollider(other))
            objectInside = false;
    }

    private bool IsCurrentObjectCollider(Collider other) {
        if (currentObject == null || other == null)
            return false;

        return other.gameObject == currentObject ||
               other.transform.IsChildOf(currentObject.transform);
    }
}