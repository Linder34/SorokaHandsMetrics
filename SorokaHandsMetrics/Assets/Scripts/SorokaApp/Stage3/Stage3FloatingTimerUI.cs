using TMPro;
using UnityEngine;

public class Stage3FloatingTimerUI : MonoBehaviour {
    [Header("References")]
    [SerializeField] private TMP_Text timerText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color expiredColor = Color.red;

    private void Awake() {
        Hide();
    }

    private void OnDisable() {
        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    public void SetFollowTarget(Transform target) {
        // Intentionally empty.
        // Timer stays where it is placed in the scene.
    }

    public void Show() {
        gameObject.SetActive(true);

        if (timerText != null)
            timerText.gameObject.SetActive(true);
    }

    public void Hide() {
        if (timerText != null)
            timerText.gameObject.SetActive(false);

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