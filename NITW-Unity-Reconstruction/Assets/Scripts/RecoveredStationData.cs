using System;
using UnityEngine;
[Serializable] public sealed class RecoveredStationData {
    public RecoveredStationNode[] nodes;
    public RecoveredStationSprite[] sprites;
    public RecoveredStationTexture[] textures;
    public RecoveredStationMaterial[] materials;
    public RecoveredStationMesh[] meshes;
    public RecoveredStationSound[] sounds;
    public Vector3 camera;
    public float fov;
    public Color background;
    public int mask;
}
[Serializable] public sealed class RecoveredStationNode {
    public int id,parent,layer;
    public string name,mesh;
    public bool active;
    public RecoveredTitleTransform transform;
    public RecoveredStationRenderer sprite,meshRenderer;
}
[Serializable] public sealed class RecoveredStationRenderer {
    public string sprite,material;
    public bool enabled,flipX,flipY;
    public Color color;
    public int sortingLayer,order;
}
[Serializable] public sealed class RecoveredStationSprite { public string key,name;public float ppu;public Vector2 pivot; }
[Serializable] public sealed class RecoveredStationTexture { public string key,name; }
[Serializable] public sealed class RecoveredStationMaterial {
    public string key,name,shader,texture;
    public Color color,tint;
    public Vector2 scale,offset;
}
[Serializable] public sealed class RecoveredStationMesh { public string key,name;public Vector3[] vertices;public Vector2[] uv;public int[] triangles; }
[Serializable] public sealed class RecoveredStationSound { public string @event;public Vector3 position;public bool spatial; }
