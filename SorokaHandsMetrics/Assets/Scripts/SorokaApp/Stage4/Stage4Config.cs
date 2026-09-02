using System;
using UnityEngine;

public enum Stage4RingSize {
    Large,
    Medium,
    Small
}

public enum Stage4PathHeight {
    Low,
    Medium,
    High
}

public enum Stage4PathType {
    Straight,
    Arch,
    ZigZag,
    SideCurve,
    SCurve,
    AlternatingTower
}

[Serializable]
public class Stage4Config {

    [Header("Hand")]
    public HandSelection handSelection =
        HandSelection.Right;


    [Header("Session")]
    public bool endlessMode = false;
    public int trialsAmount = 10;

    public bool useTimeLimit = false;
    public float timeLimitSeconds = 5f;


    [Header("Target")]
    public TargetDistance targetDistance =
        TargetDistance.Medium;

    public TargetSize targetSize =
        TargetSize.Medium;


    [Header("Path")]
    public Stage4RingSize ringSize =
        Stage4RingSize.Large;

    public Stage4PathHeight pathHeight =
        Stage4PathHeight.Medium;

    /*
     * PathController changes this automatically
     * at the beginning of each cycle.
     *
     * Later we can expose path selection in the UI.
     */
    public Stage4PathType pathType =
        Stage4PathType.Arch;


    public int GetEffectiveTrials() {
        return endlessMode
            ? int.MaxValue
            : trialsAmount;
    }


    public void ClampValues() {

        trialsAmount =
            Mathf.Clamp(
                trialsAmount,
                1,
                100);

        timeLimitSeconds =
            Mathf.Clamp(
                timeLimitSeconds,
                1f,
                60f);
    }
}