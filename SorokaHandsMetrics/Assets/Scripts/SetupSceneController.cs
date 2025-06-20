using UnityEngine;
using UnityEngine.SceneManagement;


public class SetupSceneController : MonoBehaviour {
    [Header("Target Objects Container")]
    [Tooltip("Assign the parent GameObject that contains all target objects as children.")]
    public Transform objectsParent;

    [Header("Scene Names")]
    public string mainSceneName = "MainScene";
    public string setupSceneName = "SetupScene";
    public string mainMenuSceneName = "MainMenuScene";

    void Start() {
        ShowRealObjects();
    }

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

        /// <summary>
        /// Loops through every child of objectsParent and sets it active.
        /// </summary>
    public void ShowRealObjects() {
        if (objectsParent == null) {
            Debug.LogWarning("SetupSceneController: objectsParent is not assigned!");
            return;
        }

        foreach (Transform child in objectsParent) {
            if (child.name == "Real Phone" ||
                child.name == "Real Wallet" ||
                child.name == "Real Bottle") {
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.enabled = true;
            }
        }
    }
}