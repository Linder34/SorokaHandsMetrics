using UnityEngine;

public class RehabMenuController : MonoBehaviour {
    [Header("UI Roots")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject stage1SettingsRoot;
    [SerializeField] private GameObject stage2SettingsRoot;
    [SerializeField] private GameObject stage3SettingsRoot;
    [SerializeField] private GameObject stage4SettingsRoot;

    private void Start() {
        EnterMainMenu();
    }

    public void EnterMainMenu() {
        SetOnlyMainMenu();
    }

    public void OpenStage1Settings() {
        SetOnlyStage1Settings();
    }

    public void OpenStage2Settings() {
        SetOnlyStage2Settings();
    }

    public void OpenStage3Settings() {
        SetOnlyStage3Settings();
    }

    public void OpenStage4Settings() {
        SetOnlyStage4Settings();
    }

    public void StartStage1() {
        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);
    }

    public void StartStage2() {
        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(false);
    }

    public void StartStage3() {
        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(false);
    }

    public void StartStage4() {
        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(false);
    }

    public void EnterStage1Settings() {
        SetOnlyStage1Settings();
    }

    public void EnterStage2Settings() {
        SetOnlyStage2Settings();
    }

    public void EnterStage3Settings() {
        SetOnlyStage3Settings();
    }

    public void EnterStage4Settings() {
        SetOnlyStage4Settings();
    }

    private void SetOnlyMainMenu() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);

        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(false);

        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(false);

        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(false);
    }

    private void SetOnlyStage1Settings() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(true);

        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(false);

        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(false);

        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(false);
    }

    private void SetOnlyStage2Settings() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);

        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(true);

        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(false);

        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(false);
    }

    private void SetOnlyStage3Settings() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);

        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(false);

        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(true);

        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(false);
    }

    private void SetOnlyStage4Settings() {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (stage1SettingsRoot != null)
            stage1SettingsRoot.SetActive(false);

        if (stage2SettingsRoot != null)
            stage2SettingsRoot.SetActive(false);

        if (stage3SettingsRoot != null)
            stage3SettingsRoot.SetActive(false);

        if (stage4SettingsRoot != null)
            stage4SettingsRoot.SetActive(true);
    }
}