using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    [Header("Scene Names")]
    public string mainSceneName = "MainScene";
    public string setupSceneName = "SetupScene";
    public string mainMenuSceneName = "MainMenuScene";

    // Called by StartButton
    public void StartMainScene() {
        SceneManager.LoadScene(mainSceneName);
    }

    // Called by SetupButton
    public void OpenSetup() {
        SceneManager.LoadScene(setupSceneName);
    }

    // Called by MainMenuButton
    public void MainMenuScene() {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Called by ExitButton
    public void ExitApplication() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}