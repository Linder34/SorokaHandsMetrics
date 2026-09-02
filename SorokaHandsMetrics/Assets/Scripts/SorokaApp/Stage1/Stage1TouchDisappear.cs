using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stage1TouchDisappear : MonoBehaviour {
    [Header("Touch Detection")]
    [SerializeField] private float touchThresholdMeters = 0.015f;

    [Header("Pop Effect")]
    [SerializeField] private AudioClip popSound;
    [SerializeField] private float popDuration = 0.25f;
    [SerializeField] private float scaleUpMultiplier = 1.4f;

    private OVRSkeleton skeleton;
    private Stage1CycleController controller;
    private Collider targetCollider;

    private readonly List<Transform> fingertipBones = new List<Transform>();

    private bool initialized;
    private bool hasPopped;

    private Material materialInstance;
    private Color originalColor;

    public void Initialize(OVRSkeleton handSkeleton, Stage1CycleController cycleController) {
        skeleton = handSkeleton;
        controller = cycleController;
        targetCollider = GetComponent<Collider>();

        fingertipBones.Clear();
        initialized = false;
        hasPopped = false;

        if (skeleton == null || controller == null || targetCollider == null)
            return;

        CacheFingertips();

        if (fingertipBones.Count == 0)
            return;

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) {
            materialInstance = rend.material;
            originalColor = materialInstance.color;
        }

        initialized = true;
    }

    private void Update() {
        if (!initialized || !gameObject.activeInHierarchy)
            return;

        bool isTouching = IsAnyFingertipTouching();

        // Only report touch state.
        // The controller decides when the cycle is actually successful.
        controller.SetCurrentObjectTouchState(gameObject, isTouching);
    }

    public void Pop() {
        if (!initialized || hasPopped || !gameObject.activeInHierarchy)
            return;

        StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine() {
        hasPopped = true;

        if (popSound != null)
            AudioSource.PlayClipAtPoint(popSound, transform.position, 0.8f);

        Vector3 originalScale = transform.localScale;
        float t = 0f;

        while (t < popDuration) {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popDuration);

            transform.localScale = Vector3.Lerp(originalScale, originalScale * scaleUpMultiplier, p);

            if (materialInstance != null) {
                Color c = originalColor;
                c.a = Mathf.Lerp(1f, 0f, p);
                materialInstance.color = c;
            }

            yield return null;
        }

        gameObject.SetActive(false);

        transform.localScale = originalScale;

        if (materialInstance != null)
            materialInstance.color = originalColor;

        hasPopped = false;
    }

    private bool IsAnyFingertipTouching() {
        foreach (var tip in fingertipBones) {
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