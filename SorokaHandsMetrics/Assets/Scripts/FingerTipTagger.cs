// Assets/Scripts/FingerTipTagger.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;  // or your OVRSkeleton namespace

[RequireComponent(typeof(OVRSkeleton))]
public class FingerTipTagger : MonoBehaviour {
    [Header("Debug Target")]
    [Tooltip("GameObject (e.g. your cube) to tint for success/fail.")]
    public GameObject debugTarget;

    [Header("Feedback Colors")]
    [Tooltip("Color when all 5 tips are found.")]
    public Color successColor = Color.blue;
    [Tooltip("Color when fewer than 5 tips are found.")]
    public Color failureColor = Color.black;

    [Header("Visualize Tip Positions")]
    [Tooltip("Diameter of spheres marking each tip.")]
    public float debugSphereSize = 0.02f;

    OVRSkeleton _skeleton;

    static readonly OVRSkeleton.BoneId[] _tipBones = new[]
    {
        OVRSkeleton.BoneId.Hand_IndexTip,
        OVRSkeleton.BoneId.Hand_MiddleTip,
        OVRSkeleton.BoneId.Hand_RingTip,
        OVRSkeleton.BoneId.Hand_PinkyTip,
        OVRSkeleton.BoneId.Hand_ThumbTip
    };

    IEnumerator Start() {
        _skeleton = GetComponent<OVRSkeleton>();
        // wait for skeleton + capsules
        yield return new WaitUntil(() => _skeleton.Bones != null && _skeleton.Bones.Count > 0);
        yield return null;

        var found = new List<Transform>();
        foreach (var bone in _skeleton.Bones) {
            if (System.Array.IndexOf(_tipBones, bone.Id) < 0) continue;

            // find or add capsule
            var caps = bone.Transform.GetComponentInChildren<CapsuleCollider>();
            if (caps == null) {
                caps = bone.Transform.gameObject.AddComponent<CapsuleCollider>();
                caps.direction = 2;
                caps.height = 0.02f;
                caps.radius = 0.005f;
                caps.isTrigger = false;
            }

            caps.tag = "FingerTip";
            found.Add(caps.transform);

            // debug sphere
            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sph.GetComponent<Collider>());
            sph.transform.SetParent(caps.transform, false);
            sph.transform.localScale = Vector3.one * debugSphereSize;
            sph.GetComponent<Renderer>().material.color = successColor;
        }

        bool ok = found.Count == _tipBones.Length;
        if (debugTarget != null) {
            var rend = debugTarget.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = ok ? successColor : failureColor;
        }

        Debug.Log($"[FingerTipTagger] found {found.Count}/5 tips → OK={ok}");
    }
}
