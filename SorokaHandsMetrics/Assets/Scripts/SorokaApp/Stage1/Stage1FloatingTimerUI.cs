using TMPro;
using UnityEngine;

public class Stage1FloatingTimerUI : MonoBehaviour {
    [Header("References")]
    [SerializeField] private TMP_Text timerText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color expiredColor = Color.red;

    private bool isVisible = false;

    public void SetFollowTarget(Transform target) {
        // Intentionally left empty now.
        // We no longer follow the camera.
    }

    public void Show() {
        isVisible = true;
        gameObject.SetActive(true);
    }

    public void Hide() {
        isVisible = false;
        gameObject.SetActive(false);
    }

    public void SetTime(float seconds) {
        if (timerText == null) return;

        Show();
        timerText.color = normalColor;
        timerText.text = $"Time Left: {Mathf.CeilToInt(seconds)}";
    }

    public void SetExpiredZero() {
        if (timerText == null) return;

        Show();
        timerText.color = expiredColor;
        timerText.text = "Time Left: 0";
    }
}
