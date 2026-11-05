using System;
using UnityEngine;

[Serializable] public class RecoveredTitleMenuData {
    public RecoveredTitleMenuRow[] rows;
    public string fontName;
    public float fontDefaultSize,hudFov,hudDepth,menuVisibleTime;
    public int hudClearFlags,hudMask;
    public RecoveredTitleTransform hudTransform;
}
[Serializable] public class RecoveredTitleMenuRow {
    public int id,layer,fontSize,fontStyle,anchor,alignment,sortingLayer,order;
    public string name,label,serializedText;
    public RecoveredTitleTransform transform;
    public float characterSize,lineSpacing,offsetZ;
    public bool richText;
    public Color color;
}
