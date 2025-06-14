using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    [Header("Scene Names")]
    public string MainSceneName = "MainScene";   // set this in the Inspector
    //public string setupSceneName = "SetupScene";  // set this in the Inspector

    // Called by StartButton
    public void StartMainScene() {
        SceneManager.LoadScene(MainSceneName);
    }

    // Called by SetupButton
    //public void OpenSetup() {
    //    SceneManager.LoadScene(setupSceneName);
    //}

    // Called by ExitButton
    public void ExitApplication() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}