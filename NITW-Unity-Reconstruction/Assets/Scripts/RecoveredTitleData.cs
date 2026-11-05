using System;
using UnityEngine;

[Serializable] public class RecoveredTitleData {
    public RecoveredTitleTransform parent, logo, bark, camera;
    public RecoveredTitleLeafData[] leaves;
    public RecoveredMurderflyData[] flies;
    public RecoveredTitleBloomData bloom;
    public RecoveredTitleMeshData[] meshes;
    public RecoveredTitleMaterialData[] materials;
    public float logoPPU, fov, introTime, cameraStartZ;
    public Vector2 logoPivot;
    public Color background;
    public int barkLayer, barkOrder;
}
[Serializable] public class RecoveredTitleTransform {
    public Vector3 position, scale;
    public Quaternion rotation;
    public void Apply(Transform t) { t.localPosition=position; t.localRotation=rotation; t.localScale=scale; }
}
[Serializable] public class RecoveredTitleLeafData {
    public int id, material, sortingLayer, order;
    public string name;
    public RecoveredTitleTransform transform, child;
    public RecoveredTitleLeafFields fields;
}
[Serializable] public class RecoveredTitleLeafFields {
    public RecoveredTitleRange angle, normalWaveSpeed, leanWaveSpeed;
    public float leanStrength, leanMoveAmount, leanMoveSpeed, leanInSpeed, leanOutSpeed;
    public float leanInPower, leanOutPower, waveAcceleration;
}
[Serializable] public class RecoveredTitleRange {
    public float min, max;
    [NonSerialized] private bool picked;
    [NonSerialized] private float value;
    public float GetPicked() { if(!picked) Pick(); return value; }
    public float Pick() { value=UnityEngine.Random.Range(min,max); picked=true; return value; }
}
[Serializable] public class RecoveredTitleMeshData { public int id; public string name; public Vector3[] vertices; public int[] triangles; }
[Serializable] public class RecoveredTitleMaterialData { public int id; public Color color,tint; }
[Serializable] public class RecoveredMurderflyData {
    public int id;
    public RecoveredTitleTransform transform;
    public RecoveredMurderflyFields fields;
    public RecoveredMurderflyChild[] children;
}
[Serializable] public class RecoveredMurderflyChild {
    public string name;
    public RecoveredTitleTransform transform;
    public int material,sortingLayer,order;
}
[Serializable] public class RecoveredMurderflyFields {
    public RecoveredTitleRange baseSpeed,speedVary,speedVarySpeed,wave1Speed,wave1Amount,wave2Speed,wave2Amount;
    public float scaleSpeedX,scaleTimeOffsetX,scaleAmountX,scaleSpeedY,scaleTimeOffsetY,scaleAmountY;
}
[Serializable] public class RecoveredTitleBloomData {
    public float sepBlurSpread,bloomIntensity,bloomThreshhold,lensflareIntensity;
    public Color bloomThreshholdColor;
    public int bloomBlurIterations,quality,hdr,screenBlendMode;
}
