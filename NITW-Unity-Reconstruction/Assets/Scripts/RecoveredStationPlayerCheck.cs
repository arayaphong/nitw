using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs only with -station-player-check <directory>. Inputs travel through the
// same controller update as keyboard input; no simulated position changes are
// used to pass movement, collision, jump, or camera assertions.
public sealed class RecoveredStationPlayerCheck : MonoBehaviour {
    [Serializable] private sealed class Assertion { public string name; public bool passed; }
    [Serializable] private sealed class Measurement { public string name; public float value; }
    [Serializable] private sealed class Report {
        public string scope="Menu -> Thai IntroPoem -> BusStation; movement, animation, collision, camera";
        public int failures;
        public List<Assertion> assertions=new List<Assertion>();
        public List<Measurement> measurements=new List<Measurement>();
    }
    private readonly Report report=new Report();
    private string output;
    private RecoveredStation station;
    private RecoveredStationPlayer player;
    private float lastPeak,lastFlightTime;
    private bool lastLanded;

    public void Configure(string path) {output=path;Directory.CreateDirectory(path);}
    private void Check(bool value,string name) {
        report.assertions.Add(new Assertion {name=name,passed=value});
        if(!value) {report.failures++;Debug.LogError("NITW_STATION_PLAYER_CHECK_FAILURE "+name);}
    }
    private void Measure(string name,float value) {report.measurements.Add(new Measurement {name=name,value=value});}
    private void Input(float x=0,bool pressed=false,bool held=false,bool down=false) {
        player.SetDiagnosticInput(x,pressed,held,down);
    }
    private IEnumerator Start() {
        float deadline=Time.realtimeSinceStartup+120;
        while(FindFirstObjectByType<TitlePrototype>()==null&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(6);
        var menu=FindFirstObjectByType<TitlePrototype>();
        Check(menu!=null,"title menu available");
        if(menu==null) {Finish();yield break;}
        menu.Select(0);menu.ActivateSelected();
        while(SceneManager.GetActiveScene().name!="IntroPoem"&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(SceneManager.GetActiveScene().name=="IntroPoem","Start New Game loads IntroPoem");
        if(SceneManager.GetActiveScene().name!="IntroPoem") {Finish();yield break;}
        var poem=FindFirstObjectByType<RecoveredPoem>();
        Time.timeScale=16;
        while(poem!=null&&SceneManager.GetActiveScene().name=="IntroPoem"&&Time.realtimeSinceStartup<deadline) {
            if(poem.AwaitingAdvance) {
                poem.Advance();
                do {yield return null;}while(poem!=null&&poem.AwaitingAdvance&&Time.realtimeSinceStartup<deadline);
            } else if(poem.AwaitingChoice) {
                while(poem!=null&&poem.Typing&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSeconds(1.1f);
                if(poem!=null)poem.Advance();
                do {yield return null;}while(poem!=null&&poem.AwaitingChoice&&Time.realtimeSinceStartup<deadline);
            } else yield return null;
        }
        Time.timeScale=1;
        Check(SceneManager.GetActiveScene().name=="BusStation","IntroPoem loads BusStation");
        if(SceneManager.GetActiveScene().name!="BusStation") {Finish();yield break;}
        station=FindFirstObjectByType<RecoveredStation>();
        player=FindFirstObjectByType<RecoveredStationPlayer>();
        Check(station!=null&&player!=null,"station and movement controller present");
        if(station==null||player==null) {Finish();yield break;}
        player.DiagnosticInput=true;Input();
        while((!station.Ready||!player.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(station.Ready&&player.Ready,"iris completes and controller becomes ready");
        yield return Drive(0,.4f);
        Check(player.Grounded,"initial pose snapped to source floor");
        Check(station.AudioReady&&station.NativeErrors==0,"station ambient audio remains healthy");
        Check(player.Animation!=null,"original Mae animation runtime connected");
        float floorY=player.transform.position.y;
        Vector3 start=player.transform.position;
        Measure("initialFloorY",floorY);
        CheckIdleHead("on entry");
        Capture("station-player-idle.png");
        yield return Drive(0,7);
        CheckIdleHead("after idle and blink cycles");

        // A half-axis input is below the original .6 run threshold.
        yield return Drive(-.5f,.7f);
        Check(player.transform.position.x<start.x-.8f,"half-axis walks left");
        Check(Mathf.Abs(Mathf.Abs(player.Velocity.x)-2.5f)<.08f,"walk speed reaches original 2.5");
        Check(player.Animation!=null&&player.Animation.CurrentState.Contains("Walk"),"walking selects recovered Walk animation");
        Measure("walkSpeed",Mathf.Abs(player.Velocity.x));
        yield return Drive(0,.4f);
        CheckIdleHead("after walking");
        Capture("station-player-idle-after-walk.png");
        float cameraBefore=station.sceneCamera.transform.position.x;
        yield return Drive(-1,.8f);
        Check(Mathf.Abs(Mathf.Abs(player.Velocity.x)-4.25f)<.08f,"full-axis reaches original run speed 4.25");
        Check(player.Animation!=null&&player.Animation.CurrentState.Contains("Run"),"running selects recovered Run animation");
        Measure("runSpeed",Mathf.Abs(player.Velocity.x));
        Check(station.sceneCamera.transform.position.x<cameraBefore-.5f,"camera follows horizontal movement");
        string firstRun=VisibleSprites();
        Capture("station-player-run.png");
        yield return Drive(-1,.13f);
        Check(VisibleSprites()!=firstRun,"run animation advances original sprite frames");
        Check(Mathf.Abs(station.sceneCamera.transform.position.y-.15f)<.003f,"camera keeps source Y lock while moving");
        yield return Drive(0,.4f);
        Check(Mathf.Abs(player.Velocity.x)<.001f,"source friction stops horizontal movement");
        CheckIdleHead("after running");
        Capture("station-player-idle-after-run.png");

        // Turn is a source clip with gameplay callbacks, rather than an
        // instantaneous mirror. Measure both its intermediate event and the
        // completion event against their recovered clip times.
        player.ResetDiagnosticPose(new Vector3(0,floorY,0));
        yield return Drive(0,.2f);
        int originalFacing=player.Facing,turnDirection=-originalFacing;
        float turnStarted=Time.time,turnedAroundAt=-1,turnCompleteAt=-1;
        Action<string> turnEvents=name=> {
            if(name=="OnTurnedAround"&&turnedAroundAt<0)turnedAroundAt=Time.time-turnStarted;
            if(name=="OnTurnAnimationComplete"&&turnCompleteAt<0)turnCompleteAt=Time.time-turnStarted;
        };
        player.Animation.SourceEvent+=turnEvents;
        yield return Drive(turnDirection,.3f);
        Check(player.Animation.CurrentState.Contains("Turn"),"reversing direction plays original Turn clip");
        Check(player.Facing==originalFacing,"turn keeps original facing before source turn callback");
        Capture("station-player-turn.png");
        while(Time.time-turnStarted<1.35f) {Input(turnDirection);yield return null;}
        player.Animation.SourceEvent-=turnEvents;
        Check(turnedAroundAt>=0&&Mathf.Abs(turnedAroundAt-.46666667f)<.07f,"Turn emits OnTurnedAround at original .4667 seconds");
        Check(turnCompleteAt>=0&&Mathf.Abs(turnCompleteAt-1.16666663f)<.07f,"Turn completes at original 1.1667-second callback");
        Check(player.Facing==turnDirection&&player.Animation.CurrentState.Contains("Run"),"completed turn faces requested direction and resumes Run");
        Measure("turnAroundEventSeconds",turnedAroundAt);Measure("turnCompleteEventSeconds",turnCompleteAt);
        yield return Drive(0,.4f);

        // Isolate two flights at the same source floor point, away from benches.
        Vector3 testPose=new Vector3(-4,floorY,0);
        player.ResetDiagnosticPose(testPose);
        player.follow.SnapToPlayer();
        yield return Drive(0,.4f);
        float jumpFloor=player.transform.position.y;
        yield return MeasureJump(false,"station-player-jump.png");
        float tapHeight=lastPeak-jumpFloor,tapTime=lastFlightTime;
        Check(lastLanded,"tap jump lands again");
        Check(tapHeight>1.45f&&tapHeight<1.9f,"tap jump matches source gravity and impulse range");
        Check(Mathf.Abs(player.transform.position.y-jumpFloor)<.012f,"landing does not sink through floor");
        Measure("tapJumpHeight",tapHeight);Measure("tapJumpFlightTime",tapTime);
        yield return Drive(0,.4f);
        CheckIdleHead("after jumping");
        Capture("station-player-idle-after-jump.png");
        player.ResetDiagnosticPose(testPose);
        yield return Drive(0,.2f);
        jumpFloor=player.transform.position.y;
        yield return MeasureJump(true,"station-player-held-jump.png");
        float heldHeight=lastPeak-jumpFloor;
        Check(lastLanded,"held jump lands again");
        Check(heldHeight>tapHeight+.015f,"holding jump applies original apex hang gravity");
        Check(lastFlightTime>tapTime+.025f,"held jump spends longer in the air");
        Measure("heldJumpHeight",heldHeight);Measure("heldJumpFlightTime",lastFlightTime);
        Check(Mathf.Abs(station.sceneCamera.transform.position.y-.15f)<.003f,"jump does not scroll camera vertically");

        yield return Drive(0,.4f);
        player.ResetDiagnosticPose(testPose);
        yield return Drive(0,.2f);
        Input(0,true,false);yield return null;Input();
        bool airborne=false,buffered=false,rebounded=false;
        float bufferStart=Time.time;
        while(Time.time-bufferStart<3) {
            airborne|=!player.Grounded;
            if(airborne&&!buffered&&player.Velocity.y<0&&player.HeightOffGround>0&&player.HeightOffGround<.25f) {
                Input(0,true,false);buffered=true;
            } else Input();
            if(buffered&&player.Velocity.y>5) {rebounded=true;break;}
            yield return null;
        }
        Check(buffered&&rebounded,"jump pressed shortly before landing is buffered");
        yield return Drive(0,1.6f);

        // Preserve the consecutive-jump state between all three flights. The
        // original .25-second landing window advances variants 1 -> 2 -> 3;
        // only the third uses impulse 13 instead of 10.
        player.ResetDiagnosticPose(new Vector3(-8,floorY,0));
        yield return Drive(.5f,.15f);
        float firstChainHeight=0,thirdChainHeight=0;
        for(int variant=1;variant<=3;variant++) {
            int jumpsBefore=player.JumpCount,landingsBefore=player.LandingCount;
            float chainStart=Time.time,chainFloor=player.transform.position.y,chainPeak=chainFloor;
            Input(.5f,true,false);yield return null;
            Check(player.JumpCount==jumpsBefore+1&&player.JumpVariant==variant,"consecutive jump selects original variant "+variant);
            Check(player.Animation.CurrentState.Contains("JumpRight"+variant),"consecutive jump uses recovered right-facing clip "+variant);
            float launchSpeed=player.Velocity.y;
            Measure("chainJump"+variant+"LaunchSpeed",launchSpeed);
            Check(variant==3?launchSpeed>11.5f&&launchSpeed<=13.01f:launchSpeed>8.5f&&launchSpeed<=10.01f,
                "consecutive jump "+variant+" uses original vertical impulse");
            bool chainLanded=false;
            while(Time.time-chainStart<3) {
                Input(.5f);
                chainPeak=Mathf.Max(chainPeak,player.transform.position.y);
                if(player.LandingCount>landingsBefore&&player.Grounded) {chainLanded=true;break;}
                yield return null;
            }
            Check(chainLanded,"consecutive jump "+variant+" lands through normal collision path");
            Measure("chainJump"+variant+"Height",chainPeak-chainFloor);
            if(variant==1)firstChainHeight=chainPeak-chainFloor;
            if(variant==3) {thirdChainHeight=chainPeak-chainFloor;Capture("station-player-triple-jump-land.png");}
            if(!chainLanded)break;
            if(variant<3)yield return Drive(.5f,.04f);
        }
        Check(thirdChainHeight>firstChainHeight+.9f,"third consecutive jump reaches original higher arc");
        yield return Drive(0,.4f);

        // The original bench has a one-way surface, so ascent passes through it
        // and descent stands on it. This uses its serialized center, not an
        // invented blocking volume. The floor height is measured above.
        player.ResetDiagnosticPose(new Vector3(5.95f,floorY,0));
        yield return Drive(0,.2f);
        float benchFloor=player.transform.position.y;
        yield return MeasureJump(false,"station-player-bench-jump.png");
        float benchY=player.transform.position.y;
        Check(lastLanded&&benchY>benchFloor+.2f,"jump rises through and lands on one-way bench");
        Measure("benchSurfaceY",benchY);
        Capture("station-player-bench.png");
        Input(0,true,false,true);yield return null;
        yield return Drive(0,.8f);
        Check(player.Grounded&&Mathf.Abs(player.transform.position.y-benchFloor)<.02f,"down plus jump falls through bench onto floor");

        // Traverse the room through the real movement controller. Exit and
        // interaction systems remain disabled at this milestone.
        player.ResetDiagnosticPose(new Vector3(-4,floorY,0));
        yield return Drive(0,.2f);
        yield return DriveToBlock(-1,10);
        float leftX=player.transform.position.x;
        Measure("leftBlockX",leftX);
        Check(leftX<-9&&leftX>-11.1f,"Janitor obstruction blocks the left route");
        yield return Drive(-1,.4f);
        Check(Mathf.Abs(player.transform.position.x-leftX)<.012f,"continued input cannot cross Janitor obstruction");
        yield return Drive(0,.5f);
        Check(CameraWithinLimits(),"camera respects left source constraint");
        Capture("station-player-left-boundary.png");
        yield return DriveToBlock(1,10);
        float rightX=player.transform.position.x;
        Measure("rightBlockX",rightX);
        Check(rightX>10.5f&&rightX<11.2f,"source right wall stops player");
        yield return Drive(1,.4f);
        Check(Mathf.Abs(player.transform.position.x-rightX)<.012f,"continued input cannot cross right wall");
        yield return Drive(0,.5f);
        Check(CameraWithinLimits(),"camera respects right source constraint");
        CheckIdleHead("facing right");
        Check(player.Grounded,"player remains supported after room traversal");
        Capture("station-player-right-boundary.png");
        Check(SceneManager.GetActiveScene().name=="BusStation","movement does not load the next scene");
        Check(FindFirstObjectByType<RecoveredPoem>()==null,"poem scene remains unloaded");
        Check(station.NativeErrors==0,"native audio reports no errors during movement");
        Input();
        Finish();
    }

    private void CheckIdleHead(string label) {
        var head=player.Animation.transform.Find("Rig/Head/HeadBack").GetComponent<SpriteRenderer>();
        var node=Array.Find(station.source.nodes,n=>n.id==236);
        var source=Array.Find(station.source.sprites,s=>s.key==node.sprite.sprite);
        var sprite=head.sprite;
        var pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
        Check(player.Animation.CurrentState.Contains("Idle"),"idle pose active "+label);
        Check(Vector2.Distance(pivot,source.pivot)<.00001f,"head uses native sprite pivot "+label);
        var center=new Vector3((.5f-source.pivot.x)*sprite.rect.width/source.ppu,
            (.5f-source.pivot.y)*sprite.rect.height/source.ppu,0);
        var expected=head.transform.TransformPoint(center);
        Check(Vector3.Distance(head.bounds.center,expected)<.0001f,"rendered head position matches native pivot "+label);
    }
    private IEnumerator Drive(float horizontal,float duration) {
        float end=Time.time+duration;
        while(Time.time<end) {Input(horizontal);yield return null;}
    }
    private IEnumerator DriveToBlock(float direction,float maxDuration) {
        float end=Time.time+maxDuration,lastX=player.transform.position.x,still=0;
        while(Time.time<end) {
            Input(direction);yield return null;
            float x=player.transform.position.x;
            still=Mathf.Abs(x-lastX)<.0005f?still+Time.deltaTime:0;lastX=x;
            if(still>.5f)yield break;
        }
    }
    private IEnumerator MeasureJump(bool held,string capture) {
        float start=Time.time,baseY=player.transform.position.y;
        lastPeak=baseY;lastFlightTime=0;lastLanded=false;
        bool airborne=false,captured=false;
        Input(0,true,held);yield return null;
        while(Time.time-start<3) {
            Input(0,false,held);
            airborne|=!player.Grounded;
            lastPeak=Mathf.Max(lastPeak,player.transform.position.y);
            if(airborne&&!captured&&player.Velocity.y<2) {
                Check(player.Animation!=null&&player.Animation.CurrentState.Contains("Jump"),"flight uses recovered Jump animation: "+capture);
                Capture(capture);captured=true;
            }
            if(airborne&&player.Grounded) {lastLanded=true;lastFlightTime=Time.time-start;break;}
            yield return null;
        }
        Input();
        if(!lastLanded)lastFlightTime=Time.time-start;
    }
    private bool CameraWithinLimits() {
        float x=station.sceneCamera.transform.position.x;
        Vector2 limits=player.follow.HorizontalLimits;
        return x>=limits.x-.03f&&x<=limits.y+.03f&&Mathf.Abs(station.sceneCamera.transform.position.y-.15f)<.003f;
    }
    private string VisibleSprites() {
        var names=new List<string>();
        foreach(var sprite in player.GetComponentsInChildren<SpriteRenderer>(true))
            if(sprite.enabled&&sprite.gameObject.activeInHierarchy&&sprite.sprite!=null)names.Add(sprite.name+":"+sprite.sprite.name);
        names.Sort();return string.Join("|",names);
    }
    private void Capture(string name) {
        var camera=station.sceneCamera;var old=camera.targetTexture;var rect=camera.rect;
        var target=new RenderTexture(960,540,24);camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=old;camera.rect=rect;Destroy(image);Destroy(target);
    }
    private void Finish() {
        Time.timeScale=1;
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true)+"\n");
        string result="NITW_STATION_PLAYER_CHECK assertions="+report.assertions.Count+" failures="+report.failures;
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-quit-after-check")>=0)Application.Quit(report.failures==0?0:1);
    }
}
