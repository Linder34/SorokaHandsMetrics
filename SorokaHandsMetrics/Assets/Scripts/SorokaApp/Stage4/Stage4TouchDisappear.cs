using System.Collections;
using UnityEngine;

public class Stage4TouchDisappear : MonoBehaviour {
    [Header("Collect Effect")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private float collectDuration = 0.3f;
    [SerializeField] private float scaleUpMultiplier = 1.25f;
    [SerializeField] private float scaleDownMultiplier = 0.15f;

    private bool isCollecting = false;

    private Material materialInstance;
    private Color originalColor;

    public void Initialize(OVRSkeleton handSkeleton, Stage4CycleController cycleController) {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) {
            materialInstance = rend.material;
            originalColor = materialInstance.color;
        }

        isCollecting = false;
    }

    public void PlaySuccessEffect() {
        if (!gameObject.activeInHierarchy || isCollecting)
            return;

        StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine() {
        isCollecting = true;

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position, 0.8f);

        Vector3 originalScale = transform.localScale;
        Vector3 peakScale = originalScale * scaleUpMultiplier;
        Vector3 finalScale = originalScale * scaleDownMultiplier;

        float firstPart = collectDuration * 0.45f;
        float secondPart = collectDuration * 0.55f;

        float t = 0f;

        while (t < firstPart) {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / firstPart);
            transform.localScale = Vector3.Lerp(originalScale, peakScale, p);
            yield return null;
        }

        t = 0f;

        while (t < secondPart) {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / secondPart);

            transform.localScale = Vector3.Lerp(peakScale, finalScale, p);

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

        isCollecting = false;
    }
}