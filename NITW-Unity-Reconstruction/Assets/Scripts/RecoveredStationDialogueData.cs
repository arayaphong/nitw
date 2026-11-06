using System;
using UnityEngine;

// Serialized presentation values and glyph references recovered from local game
// data. Narrative strings and original artwork are never embedded in this code.
[Serializable] public sealed class RecoveredStationDialogueData {
    [Serializable] public sealed class Glyph {public string character,additionalCharacters;public float width;public int[] frames;}
    [Serializable] public sealed class Image {public int id;public string name;public float ppu;public Vector2 pivot;}
    [Serializable] public sealed class IntVariable {public string name;public int value;}
    [Serializable] public sealed class Emoticon {public string emoticon;public string[] triggers;public IntVariable[] intVars;}
    [Serializable] public sealed class Speaker {
        public string name;public int root,headTracker,locator;public Vector3 headPosition,detectOffset,referenceOffset;
        public float detectRadius,bubbleScale,bubbleOpenDistance,headRadius;public Vector3 bubbleOpenDir;
    }
    public Glyph[] characters;public Image[] sprites;public int stemSprite,headTrackerGameObject;
    public Speaker[] speakers;public Emoticon[] emoticons;
    public int choiceArrowSprite,choiceDotSprite;public Vector3 choiceArrowScale,choiceDotScale;
    public float choiceArrowInset,choiceDotSpacing,choiceDotYInset,choiceTweenTime;
    public string choiceAppearEvent,choiceScrollEvent;
    public bool upperCaseOnly;
    public float spacingMultiplier,xPadding,spaceWidth,lineHeight,textScale,width,fillPadding;
    public float letterTweenTime,letterWaitTime,spaceWaitTime,punctuationWaitTime,timeMultiplier,holdToSpeedUpMultiplier;
    public Color color;
    public int wobbleFPS,sortingLayer,glyphOrder,meshOrder,stemOrder,charSoundDelay;
    public float wobbleSpeed,wobbleHeight,wobbleMultiplier,step;
    public float bubbleScale,bubbleOpenDistance,headRadius,openTime,glyphFrameTime,changeFadeMin,changeFadeTime,changeScrollDelay,resizeTime;
    public float closeTextDelay,closeFadeMin,closeFadeMax;
    public Vector3 bubbleOpenDir;
    public string textEvent,openEvent,closeEvent,changeSizeEvent;
}
