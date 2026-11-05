using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit opt-in diagnostics. No story text or assets are embedded here: line
// identity is compared against the local source-derived entry program.
public sealed class RecoveredStationNarrationCheck : MonoBehaviour {
    [Serializable] private sealed class Assertion {public string name;public bool passed;}
    [Serializable] private sealed class Report {
        public string scope="Menu -> Thai IntroPoem -> BusStation EnterLevel narration only";
        public int failures;
        public List<Assertion> assertions=new List<Assertion>();
        public List<string> displayedLineIds=new List<string>();
        public float narrationDuration;
    }
    private readonly Report report=new Report();
    private string output;
    private RecoveredStation station;
    private RecoveredStationPlayer player;
    private RecoveredStationNarration narration;
    private bool previewOnly;
    public void Configure(string path,bool previewOnly=false) {output=path;this.previewOnly=previewOnly;Directory.CreateDirectory(path);}
    private void Check(bool value,string name) {
        report.assertions.Add(new Assertion{name=name,passed=value});
        if(!value) {report.failures++;Debug.LogError("NITW_STATION_NARRATION_CHECK_FAILURE "+name);}
    }
    private IEnumerator Start() {
        float deadline=Time.realtimeSinceStartup+120;
        while(FindFirstObjectByType<TitlePrototype>()==null&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(6);
        var menu=FindFirstObjectByType<TitlePrototype>();
        Check(menu!=null,"title menu available");if(menu==null){Finish();yield break;}
        menu.Select(0);menu.ActivateSelected();
        while(SceneManager.GetActiveScene().name!="IntroPoem"&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(SceneManager.GetActiveScene().name=="IntroPoem","Start New Game loads IntroPoem");
        if(SceneManager.GetActiveScene().name!="IntroPoem"){Finish();yield break;}
        var poem=FindFirstObjectByType<RecoveredPoem>();Time.timeScale=16;
        while(poem!=null&&SceneManager.GetActiveScene().name=="IntroPoem"&&Time.realtimeSinceStartup<deadline) {
            if(poem.AwaitingAdvance) {
                poem.Advance();do{yield return null;}while(poem!=null&&poem.AwaitingAdvance&&Time.realtimeSinceStartup<deadline);
            } else if(poem.AwaitingChoice) {
                while(poem!=null&&poem.Typing&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSeconds(1.1f);if(poem!=null)poem.Advance();
                do{yield return null;}while(poem!=null&&poem.AwaitingChoice&&Time.realtimeSinceStartup<deadline);
            } else yield return null;
        }
        Time.timeScale=1;
        Check(SceneManager.GetActiveScene().name=="BusStation","IntroPoem loads BusStation");
        if(SceneManager.GetActiveScene().name!="BusStation"){Finish();yield break;}
        station=FindFirstObjectByType<RecoveredStation>();player=FindFirstObjectByType<RecoveredStationPlayer>();
        narration=FindFirstObjectByType<RecoveredStationNarration>();
        Check(station!=null&&player!=null&&narration!=null,"station, player and entry narration present");
        if(station==null||player==null||narration==null){Finish();yield break;}
        // Preview automates only the menu/poem traversal. The user receives
        // the entry stage with normal input and without diagnostic advance.
        if(previewOnly){Destroy(gameObject);yield break;}
        player.DiagnosticInput=true;narration.DiagnosticInput=true;
        player.SetDiagnosticInput(0,false,false);
        while(!player.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
        Vector3 entryPosition=player.transform.position;
        int jumpsAtEntry=player.JumpCount;
        bool lockedThroughEntry=true;
        while(!station.Ready&&Time.realtimeSinceStartup<deadline) {
            player.SetDiagnosticInput(1,true,true);
            lockedThroughEntry&=player.ControlsLocked&&Vector3.Distance(player.transform.position,entryPosition)<.002f&&player.JumpCount==jumpsAtEntry;
            yield return null;
        }
        Check(station.Ready,"source iris completes");
        while(!narration.Running&&!narration.Completed&&Time.realtimeSinceStartup<deadline) {
            player.SetDiagnosticInput(1,true,true);
            lockedThroughEntry&=player.ControlsLocked&&Vector3.Distance(player.transform.position,entryPosition)<.002f&&player.JumpCount==jumpsAtEntry;
            yield return null;
        }
        Check(lockedThroughEntry,"movement and jump are locked before the first narration line");
        Check(narration.Running&&!narration.Completed,"fresh entry starts narration automatically");
        Check(FindFirstObjectByType<RecoveredPoem>()==null,"poem is unloaded before station narration");
        Check(station.AudioReady&&station.NativeErrors==0,"station native audio remains healthy");
        Check(player.Facing==-1,"Mae faces source entry direction");
        var program=JsonUtility.FromJson<RecoveredStationNarration.Program>(narration.programAsset.text);
        string[] ids=program.lines==null?null:Array.ConvertAll(program.lines,l=>l.id);
        Check(ids!=null&&ids.Length==5,"local original EnterLevel program contains five narration lines");
        if(ids==null||ids.Length!=5){Finish();yield break;}
        Check(string.Join("|",narration.SourceLineIds)==string.Join("|",ids),"runtime source line identities match the local entry program");
        var view=narration.view;
        Check(view!=null,"source speech-bubble view is connected");
        if(view==null){Finish();yield break;}
        bool lockedThroughText=true;
        deadline=Time.realtimeSinceStartup+120;
        for(int line=0;line<ids.Length;line++) {
            while((narration.LineIndex<line||string.IsNullOrEmpty(narration.CurrentLineId))&&!narration.Completed&&Time.realtimeSinceStartup<deadline) {
                player.SetDiagnosticInput(1,true,true);yield return null;
            }
            Check(!narration.Completed&&narration.LineIndex==line,"entry visits only the next source line "+line);
            Check(narration.CurrentLineId==ids[line],"entry retains original line identity "+line);
            if(narration.Completed||narration.LineIndex!=line)break;
            report.displayedLineIds.Add(narration.CurrentLineId);
            Check(narration.CurrentText==program.lines[line].text&&view.CurrentText==program.lines[line].text,
                "view receives the exact original entry text "+line);
            Check(view.Visible,"speech bubble is visible for source line "+line);
            if(line==0&&narration.Typing) {
                string firstId=narration.CurrentLineId;
                narration.Advance();yield return null;
                Check(narration.LineIndex==0&&narration.CurrentLineId==firstId,
                    "confirmation during typing does not skip or instantly advance the source line");
                Check(narration.Typing,"early confirmation preserves progressive typing instead of revealing the whole line");
            }
            // Source confirm-hold accelerates typing; it does not reveal the
            // whole line instantly. Exercise it on the longer second line.
            narration.SetDiagnosticHeld(line==1);
            if(line==1) {
                yield return null;
                Check(Mathf.Abs(view.CurrentTimeMultiplier-view.Data.holdToSpeedUpMultiplier)<.00001f,
                    "held confirmation reaches the source typing-speed multiplier");
                Check(narration.LineIndex==line,"holding confirmation does not advance to another line");
            }
            while(narration.Typing&&Time.realtimeSinceStartup<deadline) {
                player.SetDiagnosticInput(1,true,true);
                lockedThroughText&=player.ControlsLocked&&Vector3.Distance(player.transform.position,entryPosition)<.002f&&player.JumpCount==jumpsAtEntry;
                yield return null;
            }
            narration.SetDiagnosticHeld(false);
            while(!narration.AwaitingAdvance&&!narration.Completed&&Time.realtimeSinceStartup<deadline) {
                player.SetDiagnosticInput(1,true,true);yield return null;
            }
            Check(narration.AwaitingAdvance&&!narration.Typing,"line waits for confirmation after typing "+line);
            Check(view.CanAdvance&&view.Visible,"source bubble enters its ready state "+line);
            string expected=view.Data.upperCaseOnly?program.lines[line].text.ToUpperInvariant():program.lines[line].text;
            Check(WithoutWhitespace(view.RenderedText)==WithoutWhitespace(expected),
                "rendered text preserves original punctuation and literal emphasis marks "+line);
            Check(view.VisibleGlyphCount==view.TotalGlyphCount&&view.VisibleGlyphCount>0,
                "all source glyphs are progressively revealed "+line);
            // Keep the completed page visible without any confirm input. This
            // catches timed auto-advance and accidental movement input reuse.
            string shown=narration.CurrentLineId;float heldUntil=Time.realtimeSinceStartup+.25f;
            while(Time.realtimeSinceStartup<heldUntil) {
                player.SetDiagnosticInput(1,true,true);
                lockedThroughText&=player.ControlsLocked&&Vector3.Distance(player.transform.position,entryPosition)<.002f&&player.JumpCount==jumpsAtEntry;
                yield return null;
            }
            Check(narration.CurrentLineId==shown&&!narration.Completed,"completed text waits for player confirmation "+line);
            int drawable=0;bool onScreen=true;
            foreach(var glyph in view.GetComponentsInChildren<SpriteRenderer>())if(glyph.enabled&&glyph.sprite!=null&&glyph.color.a>.5f) {
                drawable++;var screen=station.sceneCamera.WorldToViewportPoint(glyph.bounds.center);
                onScreen&=screen.z>0&&screen.x>=0&&screen.x<=1&&screen.y>=0&&screen.y<=1;
            }
            Check(drawable>0&&onScreen,"source glyph renderers are active and inside the camera "+line);
            if(line==0) {
                Check(station.IrisOpenedAt-station.IrisStartedAt>=.6f&&narration.FirstLineAt>=station.IrisOpenedAt,
                    "first narration line waits for the source opening iris");
                Check(narration.janitor.EntryStarted&&narration.janitor.DrillRequested,
                    "source entry actor setup requests the janitor drill");
                Check(station.ActiveLoopCount>0,"source janitor drill loop is active during narration");
            }
            if(line==0||line==2||line==4)Capture("station-narration-"+(line+1)+".png");
            player.SetDiagnosticInput(0,false,false);
            narration.Advance();yield return null;
        }
        while(!narration.Completed&&Time.realtimeSinceStartup<deadline)yield return null;
        player.SetDiagnosticInput(0,false,false);yield return null;
        Check(narration.Completed&&!narration.Running,"narration ends after the fifth source line");
        Check(!view.Visible&&!view.Closing,"speech bubble finishes closing before completion");
        Check(narration.DisplayedLineCount==5&&report.displayedLineIds.Count==5,"exactly five lines are displayed without repetition");
        Check(lockedThroughText,"walking and jumping input cannot move Mae during narration");
        Check(!player.ControlsLocked,"completion returns movement control");
        Check(player.JumpCount==jumpsAtEntry,"dialogue confirmation does not leak into a queued jump");
        Check(Vector3.Distance(player.transform.position,entryPosition)<.002f,"Mae stays at her source entry position until narration ends");
        report.narrationDuration=narration.CompletedAt-narration.StartedAt;
        Capture("station-narration-complete.png");
        float startX=player.transform.position.x;
        float moveEnd=Time.time+.4f;
        while(Time.time<moveEnd) {player.SetDiagnosticInput(-1,false,false);yield return null;}
        Check(player.transform.position.x<startX-.4f,"walking works after narration");
        player.SetDiagnosticInput(0,true,true);yield return null;
        player.SetDiagnosticInput(0,false,false);
        Check(player.JumpCount==jumpsAtEntry+1&&player.Jumping,"jumping works after narration");
        Check(Mathf.Abs(station.sceneCamera.transform.position.y-.15f)<.003f,"normal source camera Y lock remains active");
        Check(SceneManager.GetActiveScene().name=="BusStation","entry narration does not load another scene");
        Check(station.NativeErrors==0,"narration audio reports no native errors");
        Finish();
    }
    // Older station diagnostics explicitly drive the new entry stage before
    // testing free movement; this helper is never run in normal play.
    public static IEnumerator CompleteEntry(Action<bool> result) {
        var entry=FindFirstObjectByType<RecoveredStationNarration>();
        if(entry==null){result(false);yield break;}
        bool previous=entry.DiagnosticInput;entry.DiagnosticInput=true;
        float deadline=Time.realtimeSinceStartup+90,nextPress=0;
        while(!entry.Completed&&Time.realtimeSinceStartup<deadline) {
            if(entry.AwaitingAdvance&&!entry.Typing&&Time.realtimeSinceStartup>=nextPress) {
                entry.Advance();nextPress=Time.realtimeSinceStartup+.25f;
            }
            yield return null;
        }
        entry.DiagnosticInput=previous;result(entry.Completed);
    }
    private static string WithoutWhitespace(string text) {
        return new string(Array.FindAll((text??"").ToCharArray(),c=>!char.IsWhiteSpace(c)));
    }
    private void Capture(string name) {
        var camera=station.sceneCamera;var old=camera.targetTexture;var rect=camera.rect;
        var target=new RenderTexture(960,540,24);camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;camera.rect=rect;Destroy(image);Destroy(target);
    }
    private void Finish() {
        Time.timeScale=1;
        if(player!=null)player.SetDiagnosticInput(0,false,false);
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true)+"\n");
        string result="NITW_STATION_NARRATION_CHECK assertions="+report.assertions.Count+" failures="+report.failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-quit-after-check")>=0)Application.Quit(report.failures==0?0:1);
    }
}
