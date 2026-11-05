using System;
using UnityEngine;

[Serializable] public sealed class RecoveredOptionsData {
    public RecoveredNode[] nodes;
    public RecoveredSprite[] sprites;
    public RecoveredFont[] fonts;
    public RecoveredMenu[] menus;
    public RecoveredSortingLayer[] sortingLayers;
}
[Serializable] public sealed class RecoveredNode {
    public int id, parent, layer;
    public string name;
    public bool active;
    public Vector3 position, scale;
    public Quaternion rotation;
    public RecoveredSpriteRenderer sprite;
    public RecoveredText text;
    public RecoveredRenderer renderer;
}
[Serializable] public sealed class RecoveredSprite { public string key, name; public float ppu; public Vector2 pivot; public int width, height; }
[Serializable] public sealed class RecoveredFont { public string key, name; public float size; }
[Serializable] public sealed class RecoveredSpriteRenderer { public string asset; public Color color; public bool enabled, flipX, flipY; public int order; public long sortingLayer; }
[Serializable] public sealed class RecoveredRenderer { public bool enabled; public int order; public long sortingLayer; }
[Serializable] public sealed class RecoveredText { public string value, font; public int fontSize, anchor, alignment; public float characterSize, lineSpacing, offsetZ; public bool richText; public Color color; }
[Serializable] public sealed class RecoveredMenu { public int selector; public RecoveredItem[] items; }
[Serializable] public sealed class RecoveredItem {
    public int go, type, target, buttonType, pole, holdToConfirm, titleOnlyOption, journalOnlyOption;
    public int label, display, sliderBody, sliderNotch, arrowPrev, arrowNext, confirmLabel, yesLabel, noLabel, captureInputPopup;
    public float sliderWidth;
}

[Serializable] public sealed class RecoveredSortingLayer { public string name; public long uniqueID; }
