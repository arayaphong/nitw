using System.Collections;
using System.IO;
using UnityEngine;

// Diagnostic only: render the actual native FMOD mix to WAV and verify live
// event instances, native 3D attributes, delayed starts and the source fade.
public sealed class RecoveredFireflyAudioCheck : MonoBehaviour {
    private string output;
    private RecoveredFireflyAudio audioSystem;
    private int failures;
    public void Configure(string path,RecoveredFireflyAudio system) { output=path;audioSystem=system; }
    private void Assert(bool value,string reason) { if(!value) { failures++;Debug.LogError("NITW_FIREFLY_AUDIO_CHECK_FAILURE "+reason); } }
    private IEnumerator Start() {
        yield return new WaitForSecondsRealtime(3f);
        var motion=GetComponent<RecoveredTitleMotion>();
        Assert(audioSystem.Ready,"native ready");Assert(audioSystem.StartedVoices==3,"three event instances");
        Assert(audioSystem.OutputType==3,"native WAV mixer");
        foreach(var fly in motion.flies) {
            Assert(fly.ScheduledAudioDelay>=.75f&&fly.ScheduledAudioDelay<=1f,"source delay range");
            // Invoke may be delivered within a frame of its requested delay:
            // observed ~1.1ms early in batch mode and ~17ms in a 20-33ms live frame.
            float earlyTolerance=Mathf.Max(.005f,fly.AudioStartFrameDelta+.002f);
            float lateTolerance=Mathf.Max(.05f,fly.AudioStartFrameDelta*2f);
            Assert(fly.AudioStartAge>=fly.ScheduledAudioDelay-earlyTolerance&&fly.AudioStartAge<=fly.ScheduledAudioDelay+lateTolerance,"delayed start executed");
            var voice=fly.AudioVoice;Assert(voice!=null,"actor voice");
            if(voice==null)continue;
            Assert(audioSystem.State(voice)==0,"native playing state");
            Assert(voice.PositionUpdates>10,"follows moving actor");
            Assert((audioSystem.NativePosition(voice)-voice.LastPosition).sqrMagnitude<.000001f,"native 3D position matches submitted position");
            fly.FadeOutSound();
        }
        yield return new WaitForSecondsRealtime(.5f);
        foreach(var voice in audioSystem.Voices)Assert(audioSystem.Volume(voice)>.2f&&audioSystem.Volume(voice)<.8f,"one-second linear fade midpoint");
        yield return new WaitForSecondsRealtime(1.5f);
        foreach(var voice in audioSystem.Voices)Assert(audioSystem.Volume(voice)<.001f,"fade reaches silence");
        yield return new WaitForSecondsRealtime(.75f);
        Assert(audioSystem.NativeErrors==0,"native API errors");
        int started=audioSystem.StartedVoices, updates=audioSystem.PositionUpdates;
        audioSystem.Shutdown();
        Assert(File.Exists(Path.Combine(output,"fireflies.wav"))&&new FileInfo(Path.Combine(output,"fireflies.wav")).Length>44,"WAV mixer output");
        string result="NITW_FIREFLY_AUDIO_CHECK voices="+started+" followUpdates="+updates+" failures="+failures+" shutdown="+(!audioSystem.Ready);
        File.WriteAllText(Path.Combine(output,"result.txt"),result+"\n");Debug.Log(result);
    }
}
