using UnityEngine;

public class RehabMenuController : MonoBehaviour {
    [Header("UI Roots")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject stage1SettingsRoot;

    private void Start() {
        EnterMainMenu();
    }

    public void EnterMainMenu() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);


        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);
    }

    public void OpenStage1Settings() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(true);
    }

    public void StartStage1() {
        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);
    }
}
