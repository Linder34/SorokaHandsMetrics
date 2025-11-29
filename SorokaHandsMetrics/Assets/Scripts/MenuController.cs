using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    [Header("Scene Names")]
    public string mainSceneName = "MainScene";
    public string mainSceneName2 = "MainScene2";
    public string setupSceneName = "SetupScene";
    public string setupSceneName2 = "SetupScene2";
    public string mainMenuSceneName = "MainMenuScene";

    // Called by StartButton
    public void StartMainScene() {
        SceneManager.LoadScene(mainSceneName);
    }

    // Called by StartButton2
    public void StartMainScene2() {
        SceneManager.LoadScene(mainSceneName2);
    }

    // Called by SetupButton
    public void OpenSetup() {
        SceneManager.LoadScene(setupSceneName);
    }

    // Called by SetupButton2
    public void OpenSetup2() {
        SceneManager.LoadScene(setupSceneName2);
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