using System.Collections.Generic;
using UnityEngine;

public class Stage1TouchDisappear : MonoBehaviour {
    private OVRSkeleton skeleton;
    private Stage1CycleController controller;
    private Collider col;

    private List<Transform> tips = new List<Transform>();

    private const float threshold = 0.015f;

    public void Initialize(OVRSkeleton skel, Stage1CycleController ctrl) {
        skeleton = skel;
        controller = ctrl;
        col = GetComponent<Collider>();

        CacheTips();
    }

    private void Update() {
        if (skeleton == null || controller == null || col == null)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        foreach (var tip in tips) {
            if (tip == null) continue;

            float d = Vector3.Distance(tip.position, col.ClosestPoint(tip.position));

            if (d < threshold) {
                controller.OnObjectTouched(gameObject);
                return;
            }
        }
    }

    private void CacheTips() {
        if (skeleton == null || skeleton.Bones == null) return;

        AddTip(OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.Hand_Index3);
        AddTip(OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.Hand_Thumb3);
        AddTip(OVRSkeleton.BoneId.Hand_MiddleTip, OVRSkeleton.BoneId.Max);
        AddTip(OVRSkeleton.BoneId.Hand_RingTip, OVRSkeleton.BoneId.Max);
        AddTip(OVRSkeleton.BoneId.Hand_PinkyTip, OVRSkeleton.BoneId.Max);
    }

    private void AddTip(OVRSkeleton.BoneId primary, OVRSkeleton.BoneId fallback) {
        Transform t = GetBone(primary);

        if (t == null && fallback != OVRSkeleton.BoneId.Max)
            t = GetBone(fallback);

        if (t != null)
            tips.Add(t);
    }

    private Transform GetBone(OVRSkeleton.BoneId id) {
        foreach (var b in skeleton.Bones)
            if (b.Id == id)
                return b.Transform;

        return null;
    }
}
