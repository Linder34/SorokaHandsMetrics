// Assets/Scripts/GripEvaluator.cs
using UnityEngine;
using System.Linq;
using Oculus.Interaction;            // for InteractorState
using Oculus.Interaction.HandGrab;  // for TouchHandGrabInteractor

public class GripEvaluator : MonoBehaviour {
    [Header("Hand Interactor")]
    [Tooltip("Drag your RightHandAnchor → TouchHandGrabInteractor here.")]
    public TouchHandGrabInteractor handInteractor;

    [Header("Accuracy Settings")]
    [Range(0f, 1f)]
    public float minGripPercent = 0.01f;

    [Header("Zone Colors")]
    public Color zoneTouchedColor = Color.blue;
    public Color zoneUntouchedColor = Color.black;

    [Header("Tip Colors")]
    public Color tipTouchedColor = Color.blue;
    public Color tipUntouchedColor = Color.black;

    [Header("Result Colors")]
    public Color successColor = Color.green;
    public Color failureColor = Color.red;

    bool _hasEvaluated;
    Renderer[] _objectRenderers;
    Collider[] _fingerTips;

    void Awake() {
        // Cache cup's renderers
        _objectRenderers = GetComponentsInChildren<Renderer>();
        // Cache all fingertip colliders tagged by FingerTipTagger
        _fingerTips = GameObject
            .FindGameObjectsWithTag("FingerTip")
            .Select(go => go.GetComponent<Collider>())
            .Where(c => c != null)
            .ToArray();
    }

    void Update() {
        if (handInteractor == null)
            return;

        // When the hand first enters Select (grab), evaluate once
        if (!_hasEvaluated && handInteractor.State == InteractorState.Select) {
            EvaluateGrip();
            _hasEvaluated = true;
        }
        // Reset on release so next grab can evaluate
        else if (_hasEvaluated && handInteractor.State != InteractorState.Select) {
            _hasEvaluated = false;
        }
    }

    void EvaluateGrip() {
        // 1) Get all ghost-hand GripZone children under the cup
        var zones = GetComponentsInChildren<GripZone>(true);
        int totalZones = zones.Length;
        int touchedZones = zones.Count(z => z.IsTouched);
        float pct = totalZones > 0 ? (float)touchedZones / totalZones : 0f;
        bool pass = pct >= minGripPercent;

        // 2) Color each zone's debug sphere
        foreach (var z in zones) {
            var rz = z.GetComponentInChildren<Renderer>();
            if (rz != null)
                rz.material.color = z.IsTouched ? zoneTouchedColor : zoneUntouchedColor;
        }

        // 3) Color each fingertip's debug sphere
        foreach (var tip in _fingerTips) {
            var rt = tip.transform.GetComponentInChildren<Renderer>();
            if (rt != null) {
                bool used = zones.Any(z => z.TouchingColliders.Contains(tip));
                rt.material.color = used ? tipTouchedColor : tipUntouchedColor;
            }
        }

        // 4) Tint the cup itself green/red
        foreach (var r in _objectRenderers) {
            var mat = r.material;
            var c = pass ? successColor : failureColor;
            if (mat.HasProperty("_Color"))
                mat.color = c;
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
        }

        Debug.Log($"[GripEvaluator] {touchedZones}/{totalZones} zones ({pct:P0}) → {(pass ? "PASS" : "FAIL")}");
    }
}
