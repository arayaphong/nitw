using System;
using System.Collections.Generic;
using UnityEngine;

// Localized text and pre-shaped Thai glyphs. HarfBuzz positioning is baked at
// build preparation time so runtime rendering needs neither OS fonts nor shaping
// libraries. Original TextMesh data still supplies the recovered row geometry.
public sealed class RecoveredPoemThai : MonoBehaviour {
    [Serializable] public sealed class Entry { public string id,text; }
    [Serializable] public sealed class Translation { public string locale;public Entry[] entries; }
    [Serializable] public sealed class Glyph { public float x,y,width,height;public int end;public Rect uv; }
    [Serializable] public sealed class Run { public string text;public int[] ends;public float[] widths;public Glyph[] glyphs; }
    [Serializable] public sealed class Shaping { public string fontName;public float worldPerPixel;public Run[] runs; }
    public TextAsset translationAsset,shapingAsset;
    public Texture2D atlas;
    public Material glyphMaterial;
    public float baselineOffset;
    private Dictionary<string,string> entries;
    private Dictionary<string,Run> runs;
    public Shaping Data { get; private set; }
    private void Awake() { Init(); }
    private void Init() {
        if(entries!=null)return;
        entries=new Dictionary<string,string>();runs=new Dictionary<string,Run>();
        foreach(var entry in JsonUtility.FromJson<Translation>(translationAsset.text).entries)entries.Add(entry.id,entry.text);
        Data=JsonUtility.FromJson<Shaping>(shapingAsset.text);
        foreach(var run in Data.runs)runs.Add(run.text,run);
    }
    public string Translate(string id) {
        Init();if(!entries.TryGetValue(id,out var value))throw new InvalidOperationException("Missing Thai line: "+id);
        return value;
    }
    public int[] PrefixEnds(string text) { Init();return runs[text].ends; }
    public void Prepare(TextMesh text,string fullText) {
        Init();
        var view=text.GetComponent<RecoveredThaiLine>();
        if(view==null)view=text.gameObject.AddComponent<RecoveredThaiLine>();
        view.Prepare(this,text,runs[fullText]);
    }
    public static Vector3 Size(TextMesh text) {
        var view=text.GetComponent<RecoveredThaiLine>();
        return view!=null?view.Size:text.GetComponent<MeshRenderer>().bounds.size;
    }
}

public sealed class RecoveredThaiLine : MonoBehaviour {
    private RecoveredPoemThai library;
    private RecoveredPoemThai.Run run;
    private TextMesh native;
    private Mesh mesh;
    private MeshRenderer renderer;
    private Material material;
    private int lastLength=-1,visible;
    public Vector3 Size { get {
        float width=visible>0?run.widths[visible-1]*library.Data.worldPerPixel:0;
        return new Vector3(width,.29464f,0);
    } }
    public void Prepare(RecoveredPoemThai data,TextMesh text,RecoveredPoemThai.Run shaped) {
        library=data;native=text;run=shaped;lastLength=-1;visible=0;
        native.GetComponent<MeshRenderer>().enabled=false;
        if(mesh==null) {
            var child=new GameObject("Thai shaped glyphs");child.transform.SetParent(transform,false);
            mesh=new Mesh();mesh.name="Thai poem glyphs";mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=child.AddComponent<MeshRenderer>();
            material=new Material(data.glyphMaterial);renderer.sharedMaterial=material;
        }
        mesh.Clear();float scale=data.Data.worldPerPixel;
        var vertices=new Vector3[run.glyphs.Length*4];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
        for(int i=0;i<run.glyphs.Length;i++) {
            var g=run.glyphs[i];int v=i*4;
            float l=g.x*scale,r=(g.x+g.width)*scale,t=g.y*scale-data.baselineOffset,b=(g.y-g.height)*scale-data.baselineOffset;
            vertices[v]=new Vector3(l,b,0);vertices[v+1]=new Vector3(l,t,0);
            vertices[v+2]=new Vector3(r,t,0);vertices[v+3]=new Vector3(r,b,0);
            uv[v]=new Vector2(g.uv.xMin,g.uv.yMin);uv[v+1]=new Vector2(g.uv.xMin,g.uv.yMax);
            uv[v+2]=new Vector2(g.uv.xMax,g.uv.yMax);uv[v+3]=new Vector2(g.uv.xMax,g.uv.yMin);
            for(int j=0;j<4;j++)colors[v+j]=Color.white;
        }
        mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;UpdateMesh();
    }
    private void LateUpdate() { if(mesh!=null)UpdateMesh(); }
    private void UpdateMesh() {
        material.color=native.color;
        if(lastLength==native.text.Length)return;
        lastLength=native.text.Length;visible=0;
        while(visible<run.ends.Length&&run.ends[visible]<=lastLength)visible++;
        var triangles=new List<int>();
        for(int i=0;i<run.glyphs.Length;i++)if(run.glyphs[i].end<=lastLength) {
            int v=i*4;triangles.Add(v);triangles.Add(v+1);triangles.Add(v+2);
            triangles.Add(v);triangles.Add(v+2);triangles.Add(v+3);
        }
        mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
    }
    private void OnDestroy() {
        if(mesh!=null)Destroy(mesh);
        if(material!=null)Destroy(material);
    }
}
