using System;
using UnityEngine;

[Serializable] public sealed class RecoveredPoemSceneData {
    public RecoveredTitleTransform camera;
    public float fov,fontSize,fontLineSpacing,fontAscent;
    public Vector3[] columns;
    public RecoveredPoemTextData text;
    public RecoveredPoemSpriteData[] sprites;
    public RecoveredPoemNodeData[] nodes;
    public string[] keyboardConfirmFrames;
    public string windEvent,windParameter;
}
[Serializable] public sealed class RecoveredPoemTextData {
    public float m_CharacterSize,m_LineSpacing,m_OffsetZ;
    public int m_FontSize,m_FontStyle,m_Anchor,m_Alignment;
    public bool m_RichText;
}
[Serializable] public sealed class RecoveredPoemSpriteData {
    public string key,name;
    public float ppu;
    public Vector2 pivot;
}
[Serializable] public sealed class RecoveredPoemNodeData {
    public int id;
    public string name,sprite;
    public Color color;
    public RecoveredTitleTransform transform;
}
[Serializable] public sealed class RecoveredPoemProgram { public string node,sourceSha256; public RecoveredPoemStep[] steps; }
[Serializable] public sealed class RecoveredPoemStep {
    public string op,text,lineId;
    public RecoveredPoemOption[] options;
}
[Serializable] public sealed class RecoveredPoemOption { public string text,lineId; public RecoveredPoemStep[] steps; }
