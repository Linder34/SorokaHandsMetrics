using System;
using UnityEngine;

#region Enums

//public enum HandSelection {
//    Right,
//    Left,
//    Both
//}

//public enum TargetDistance {
//    Near,
//    Medium,
//    Far,
//    Random
//}

//public enum TargetSize {
//    Small,
//    Medium,
//    Large
//}

//public enum TouchMode {
//    Instant,
//    Hold
//}

#endregion

[Serializable]
public class Stage3Config {
    [Header("Hand")]
    public HandSelection handSelection = HandSelection.Right;

    [Header("Session")]
    public bool endlessMode = false;
    public int trialsAmount = 10;

    public bool useTimeLimit = false;
    public float timeLimitSeconds = 5f;

    [Header("Target")]
    public TargetDistance targetDistance = TargetDistance.Medium;
    public TargetSize targetSize = TargetSize.Medium;

    [Header("Interaction")]
    public TouchMode touchMode = TouchMode.Instant;
    public float holdDurationSeconds = 1f;

    // Optional helper (nice to have)
    public int GetEffectiveTrials() {
        return endlessMode ? int.MaxValue : trialsAmount;
    }

    // Optional validation (prevents bad values)
    public void ClampValues() {
        trialsAmount = Mathf.Clamp(trialsAmount, 1, 100);
        timeLimitSeconds = Mathf.Clamp(timeLimitSeconds, 1f, 20f);
        holdDurationSeconds = Mathf.Clamp(holdDurationSeconds, 0.1f, 5f);
    }
}
