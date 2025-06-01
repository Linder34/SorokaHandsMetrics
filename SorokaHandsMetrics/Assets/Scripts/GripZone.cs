// Assets/Scripts/GripZone.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class GripZone : MonoBehaviour {
    HashSet<Collider> _touching = new HashSet<Collider>();

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("FingerTip"))
            _touching.Add(other);
    }

    void OnTriggerExit(Collider other) {
        if (other.CompareTag("FingerTip"))
            _touching.Remove(other);
    }

    /// <summary>All fingertip colliders currently inside this zone.</summary>
    public IReadOnlyCollection<Collider> TouchingColliders => _touching;

    /// <summary>True if at least one fingertip is touching this zone.</summary>
    public bool IsTouched => _touching.Count > 0;
}
