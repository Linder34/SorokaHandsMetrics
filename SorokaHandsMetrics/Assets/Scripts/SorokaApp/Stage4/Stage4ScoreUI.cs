using TMPro;
using UnityEngine;

public class Stage4ScoreUI : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI scoreText;

    public void SetScore(int score, int maxScore, bool endless) {
        if (scoreText == null) return;

        if (endless)
            scoreText.text = $"Score: {score}";
        else
            scoreText.text = $"Score: {score}/{maxScore}";
    }

    public void SetFinalScore(int score, int maxScore, bool endless) {
        if (scoreText == null) return;

        if (endless)
            scoreText.text = $"Final Score: {score}";
        else
            scoreText.text = $"Final Score: {score} out of {maxScore}";
    }

    public void Show() {
        if (scoreText != null)
            scoreText.gameObject.SetActive(true);
    }

    public void Hide() {
        if (scoreText != null)
            scoreText.gameObject.SetActive(false);
    }
}