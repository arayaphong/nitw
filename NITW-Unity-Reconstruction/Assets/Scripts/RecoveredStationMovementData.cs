using System;
using UnityEngine;
[Serializable] public sealed class RecoveredStationMovementData {
    public RecoveredStationTuning tuning;
    public RecoveredStationObstacleData[] obstacles;
    public Vector2 boxOffset;
    public Vector3 boxHalfExtents;
    public float boxWidth,boxHeight,boxX,boxY,moveStep,moveStepY,fineMoveStep,slopeClimb;
}
[Serializable] public sealed class RecoveredStationTuning {
    public float walkSpeed,runSpeed,walkAccel,runAccel,friction,jump,tripleJump,initialFallSpeed,gravity,terminal,
        jumpGrace,jumpBuffer,inputRunThreshold,hangThreshold,hangGravityMultiplier,tripleJumpTime,downFallTime,
        fallDistanceForAnimation,fallDistanceForLandAnimation,fallDistanceForHardLanding;
}
[Serializable] public sealed class RecoveredStationObstacleData {
    public int id,surfaceAnimation,surfaceSound,depthReference;
    public string name;
    public Vector3[] points;
    public float width,height,x,y,depth;
    public bool oneWay,noFallThrough;
}
