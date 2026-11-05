"""Validate native FMOD's captured stereo mix, separately from the event API checks."""
import array,json,math,pathlib,sys,wave
root=pathlib.Path(sys.argv[1]);p=root/'fireflies.wav'
with wave.open(str(p),'rb') as w:
    channels,rate,width,frames=w.getnchannels(),w.getframerate(),w.getsampwidth(),w.getnframes()
    assert (channels,rate,width)==(2,48000,2)
    samples=array.array('h',w.readframes(frames))
def rms(values): return math.sqrt(sum(x*x for x in values)/max(1,len(values)))
peak=max(abs(x) for x in samples);first=next(i//channels for i,x in enumerate(samples) if abs(x)>2)
tail=samples[-rate:];difference=max(abs(samples[i]-samples[i+1]) for i in range(0,len(samples),2))
data=dict(seconds=frames/rate,sampleRate=rate,channels=channels,peakPCM16=peak,
    rmsLeft=rms(samples[0::2]),rmsRight=rms(samples[1::2]),maxStereoDifference=difference,
    firstAbove2LSBSeconds=first/rate,lastHalfSecondRMS=rms(tail))
assert data['seconds']>4 and peak>10 and difference>1
assert data['firstAbove2LSBSeconds']>.6 and data['lastHalfSecondRMS']<2
(root/'waveform.json').write_text(json.dumps(data,indent=2));print(json.dumps(data,indent=2))
