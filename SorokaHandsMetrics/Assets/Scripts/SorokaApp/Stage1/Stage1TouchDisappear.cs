using System.Collections.Generic;
using UnityEngine;

public class Stage1TouchDisappear : MonoBehaviour {
    [Header("Touch Detection")]
    [SerializeField] private float touchThresholdMeters = 0.015f;

    private OVRSkeleton skeleton;
    private Stage1CycleController controller;
    private Collider targetCollider;

    private readonly List<Transform> fingertipBones = new List<Transform>();
    private bool initialized = false;

    public void Initialize(OVRSkeleton handSkeleton, Stage1CycleController cycleController) {
        skeleton = handSkeleton;
        controller = cycleController;
        targetCollider = GetComponent<Collider>();

        fingertipBones.Clear();
        initialized = false;

        if (skeleton == null) {
            FileLogger.Log($"Stage1TouchDisappear: skeleton is null for {gameObject.name}");
            return;
        }

        if (controller == null) {
            FileLogger.Log($"Stage1TouchDisappear: controller is null for {gameObject.name}");
            return;
        }

        if (targetCollider == null) {
            FileLogger.Log($"Stage1TouchDisappear: {gameObject.name} has no collider.");
            return;
        }

        CacheFingertips();

        if (fingertipBones.Count == 0) {
            FileLogger.Log($"Stage1TouchDisappear: no fingertip bones found for {gameObject.name}");
            return;
        }

        initialized = true;
        FileLogger.Log($"Stage1TouchDisappear initialized for {gameObject.name}, fingertips={fingertipBones.Count}");
    }

    private void Update() {
        if (!initialized)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        bool isTouching = IsAnyFingertipTouching();
        controller.SetCurrentObjectTouchState(gameObject, isTouching);
    }

    private bool IsAnyFingertipTouching() {
        for (int i = 0; i < fingertipBones.Count; i++) {
            Transform tip = fingertipBones[i];
            if (tip == null) continue;

            Vector3 tipPosition = tip.position;
            Vector3 closestPoint = targetCollider.ClosestPoint(tipPosition);
            float distance = Vector3.Distance(tipPosition, closestPoint);

            if (distance <= touchThresholdMeters)
                return true;
        }

        return false;
    }

    private void CacheFingertips() {
        if (skeleton == null || skeleton.Bones == null)
            return;

        AddBoneIfExists(OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.Hand_Thumb3);
        AddBoneIfExists(OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.Hand_Index3);
        AddBoneIfExists(OVRSkeleton.BoneId.Hand_MiddleTip, OVRSkeleton.BoneId.Max);
        AddBoneIfExists(OVRSkeleton.BoneId.Hand_RingTip, OVRSkeleton.BoneId.Max);
        AddBoneIfExists(OVRSkeleton.BoneId.Hand_PinkyTip, OVRSkeleton.BoneId.Max);
    }

    private void AddBoneIfExists(OVRSkeleton.BoneId primary, OVRSkeleton.BoneId fallback) {
        Transform bone = GetBone(primary);

        if (bone == null && fallback != OVRSkeleton.BoneId.Max)
            bone = GetBone(fallback);

        if (bone != null)
            fingertipBones.Add(bone);
    }

    private Transform GetBone(OVRSkeleton.BoneId id) {
        if (skeleton == null || skeleton.Bones == null)
            return null;

        foreach (var bone in skeleton.Bones) {
            if (bone.Id == id)
                return bone.Transform;
        }

        return null;
    }
}