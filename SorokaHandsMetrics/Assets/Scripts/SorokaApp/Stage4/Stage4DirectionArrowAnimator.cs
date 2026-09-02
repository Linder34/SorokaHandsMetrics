using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stage4DirectionArrowAnimator : MonoBehaviour {

    [Header("Movement")]

    [Tooltip("How far the arrow travels through the ring, in meters.")]
    [SerializeField]
    private float travelDistance = 0.10f;

    [Tooltip("How long one forward movement takes.")]
    [SerializeField]
    private float travelDuration = 0.9f;

    [Tooltip("Small pause before the arrow appears again at the start.")]
    [SerializeField]
    private float resetPause = 0.05f;


    [Header("Fade")]

    [Tooltip(
        "At what percentage of the movement the arrow starts fading. " +
        "0.4 = fade begins after 40% of the movement.")]
    [Range(0f, 0.95f)]
    [SerializeField]
    private float fadeStart = 0.4f;


    [Header("Scale Fade Fallback")]

    [Tooltip(
        "Also shrink the arrow slightly while fading. " +
        "Useful if the material does not support transparency.")]
    [SerializeField]
    private bool shrinkWhileFading = true;

    [Range(0.1f, 1f)]
    [SerializeField]
    private float finalScaleMultiplier = 0.6f;


    private Vector3 startLocalPosition;
    private Vector3 startLocalScale;

    private Coroutine animationCoroutine;


    // ============================================================
    // MATERIAL FADE DATA
    // ============================================================

    private class MaterialState {
        public Material material;
        public int colorProperty;
        public Color originalColor;
    }

    private readonly List<MaterialState>
        materialStates =
            new List<MaterialState>();


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake() {

        startLocalPosition =
            transform.localPosition;

        startLocalScale =
            transform.localScale;

        CacheMaterials();
    }


    private void OnEnable() {

        ResetArrowVisual();

        animationCoroutine =
            StartCoroutine(
                AnimateArrow());
    }


    private void OnDisable() {

        if (animationCoroutine != null) {

            StopCoroutine(
                animationCoroutine);

            animationCoroutine = null;
        }

        ResetArrowVisual();
    }


    // ============================================================
    // ANIMATION
    // ============================================================

    private IEnumerator AnimateArrow() {

        while (true) {

            float elapsed = 0f;


            // Start again from the original center position.
            ResetArrowVisual();


            while (elapsed < travelDuration) {

                float normalizedTime =
                    travelDuration <= 0f
                        ? 1f
                        : elapsed / travelDuration;


                float smoothTime =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        normalizedTime);


                // ------------------------------------------------
                // MOVE
                //
                // Vector3.forward here means the parent Ring's
                // local +Z direction.
                //
                // That is exactly the same direction used by
                // Stage4RingCheckpoint.
                // ------------------------------------------------

                transform.localPosition =
                    startLocalPosition +
                    Vector3.forward *
                    travelDistance *
                    smoothTime;


                // ------------------------------------------------
                // FADE
                // ------------------------------------------------

                float fadeAmount = 0f;


                if (normalizedTime >
                    fadeStart) {

                    fadeAmount =
                        Mathf.InverseLerp(
                            fadeStart,
                            1f,
                            normalizedTime);
                }


                float alpha =
                    1f - fadeAmount;


                SetAlpha(alpha);


                // ------------------------------------------------
                // OPTIONAL SHRINK
                // ------------------------------------------------

                if (shrinkWhileFading) {

                    float scaleMultiplier =
                        Mathf.Lerp(
                            1f,
                            finalScaleMultiplier,
                            fadeAmount);


                    transform.localScale =
                        startLocalScale *
                        scaleMultiplier;
                }


                elapsed += Time.deltaTime;

                yield return null;
            }


            // Fully invisible at end.
            SetAlpha(0f);


            if (resetPause > 0f) {

                yield return new WaitForSeconds(
                    resetPause);
            }
            else {

                yield return null;
            }
        }
    }


    // ============================================================
    // RESET
    // ============================================================

    private void ResetArrowVisual() {

        transform.localPosition =
            startLocalPosition;

        transform.localScale =
            startLocalScale;

        SetAlpha(1f);
    }


    // ============================================================
    // MATERIALS
    // ============================================================

    private void CacheMaterials() {

        materialStates.Clear();


        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(
                true);


        foreach (Renderer renderer
                 in renderers) {

            if (renderer == null)
                continue;


            Material[] materials =
                renderer.materials;


            foreach (Material material
                     in materials) {

                if (material == null)
                    continue;


                int property = -1;


                // URP / newer shaders
                if (material.HasProperty(
                    "_BaseColor")) {

                    property =
                        Shader.PropertyToID(
                            "_BaseColor");
                }

                // Standard / older shaders
                else if (material.HasProperty(
                    "_Color")) {

                    property =
                        Shader.PropertyToID(
                            "_Color");
                }


                if (property == -1)
                    continue;


                MaterialState state =
                    new MaterialState();

                state.material =
                    material;

                state.colorProperty =
                    property;

                state.originalColor =
                    material.GetColor(
                        property);


                materialStates.Add(
                    state);
            }
        }
    }


    private void SetAlpha(
        float alphaMultiplier) {

        foreach (
            MaterialState state
            in materialStates) {

            if (state.material == null)
                continue;


            Color color =
                state.originalColor;


            color.a =
                state.originalColor.a *
                alphaMultiplier;


            state.material.SetColor(
                state.colorProperty,
                color);
        }
    }
}