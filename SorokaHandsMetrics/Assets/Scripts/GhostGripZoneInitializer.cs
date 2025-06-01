// Assets/Scripts/GhostGripZoneInitializer.cs
using UnityEngine;
using System.Linq;

public class GhostGripZoneInitializer : MonoBehaviour {
    [Header("Zone Collider Settings")]
    [Tooltip("Radius (meters) of each fingertip trigger zone.")]
    public float zoneRadius = 0.01f;

    [Header("Debug Visualization")]
    [Tooltip("Diameter (meters) of spheres marking each zone.")]
    public float debugSphereDiameter = 0.02f;

    [Tooltip("GameObject whose mesh(es) to tint on success/fail.")]
    public GameObject debugTarget;

    [Tooltip("Color when zone count matches markers.")]
    public Color successColor = Color.blue;

    [Tooltip("Color when zone count mismatches.")]
    public Color failureColor = Color.black;

    void Awake() {
        var markers = GetComponentsInChildren<Transform>(true)
                      .Where(t => t.name.ToLower().Contains("finger_tip_marker"))
                      .ToList();

        int created = 0;
        foreach (var m in markers) {
            var zone = new GameObject(m.name + "_Zone");
            zone.transform.SetParent(m, false);
            zone.transform.localPosition = Vector3.zero;

            var sc = zone.AddComponent<SphereCollider>();
            sc.radius = zoneRadius;
            sc.isTrigger = true;

            zone.AddComponent<GripZone>();

            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sph.GetComponent<Collider>());
            sph.transform.SetParent(zone.transform, false);
            sph.transform.localScale = Vector3.one * debugSphereDiameter;
            sph.GetComponent<Renderer>().material.color = successColor;

            created++;
        }

        bool ok = created == markers.Count;
        var target = debugTarget != null ? debugTarget : this.gameObject;
        foreach (var r in target.GetComponentsInChildren<Renderer>()) {
            var mat = r.material;
            if (mat.HasProperty("_Color"))
                mat.color = ok ? successColor : failureColor;
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", ok ? successColor : failureColor);
        }

        Debug.Log($"[GhostGripZoneInitializer] markers={markers.Count}, zones={created} → OK={ok}");
    }
}
