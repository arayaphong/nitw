"""Recover the local entry-only SpeechBubble/Main SpriteFont presentation.

Static metadata and texture decoding only; no shipped managed code is executed.
The generated resources contain original game artwork and must remain local.
"""
import os
import hashlib,json,pathlib,struct
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.Tpk import get_typetree_node
ROOT=pathlib.Path(os.environ.get("NITW_GAME_DATA", str(pathlib.Path.home() / ".local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data")))
WORK=pathlib.Path(__file__).resolve().parents[1]
DEST=WORK/'NITW-Unity-Reconstruction/Assets/Resources/RecoveredStationDialogue'
DEST.mkdir(parents=True,exist_ok=True)
g=TypeTreeGenerator('5.6.2p4');g.load_local_dll_folder(str(ROOT/'Managed'))
scene=UnityPy.load(str(ROOT/'level11'));si={o.path_id:o for o in scene.objects}
prefab=UnityPy.load(str(ROOT/'sharedassets0.assets'));pi={o.path_id:o for o in prefab.objects}
def fields(o):
 h=o.parse_monobehaviour_head();s=h.m_Script.read()
 n=g.get_nodes_up(s.m_AssemblyName,(s.m_Namespace+'.' if s.m_Namespace else '')+s.m_ClassName)
 n.m_Children=list(get_typetree_node(o.type,o.version).m_Children)+list(n.m_Children)[4:]
 return o.read_typetree(nodes=n)
font=fields(si[797]);text=fields(pi[27509]);mesh=fields(pi[25361]);character=fields(si[876])
sprites={}
def sprite(id):
 if id in sprites:return id
 s=pi[id].read();im=s.m_RD.texture.read().image
 assert im.size==(round(s.m_Rect.width),round(s.m_Rect.height)),(s.m_Name,im.size,s.m_Rect)
 im.save(DEST/(str(id)+'.png'))
 sprites[id]=dict(id=id,name=s.m_Name,ppu=s.m_PixelsToUnits,pivot=dict(x=s.m_Pivot.x,y=s.m_Pivot.y))
 return id
chars=[]
for c in font['charDatas']:
 # The entry's Main alphabet uses the shipped ASCII glyphs. Exclude unrelated
 # localized fallback aliases whose frame tables are empty.
 if len(c['character'])!=1 or ord(c['character'])>127:continue
 frames=[sprite(p['m_PathID']) for p in c['frames']]
 width=c['width']
 if width==0 and frames:
  s=pi[frames[0]].read();width=s.m_Rect.width/s.m_PixelsToUnits
 chars.append(dict(character=c['character'],additionalCharacters=c['additionalCharacters'],width=width,frames=frames))
assert font['name']=='Main' and text['align']==6 and text['width']==3 and character['bubbleTail']==1
source=dict(assemblySha256=hashlib.sha256((ROOT/'Managed/Assembly-CSharp.dll').read_bytes()).hexdigest(),
 sceneSha256=hashlib.sha256((ROOT/'level11').read_bytes()).hexdigest(),
 prefabSha256=hashlib.sha256((ROOT/'sharedassets0.assets').read_bytes()).hexdigest(),
 fontComponent=797,speechBubble=25335,spriteText=27509,speechBubbleMesh=25361,
 staticOnly=True)
speakers=[]
for name,cid,locator in [('Mae',876,875),('Janitor',839,857)]:
 c=fields(si[cid]);l=fields(si[locator])
 speakers.append(dict(name=name,root=c['m_GameObject']['m_PathID'],
  headTracker=si[c['headTracker']['m_PathID']].read().m_GameObject.m_PathID if c['headTracker']['m_PathID'] else 0,
  locator=l['m_GameObject']['m_PathID'],headPosition=c['headPosition'],detectOffset=l['detectOffset'],
  referenceOffset=l['referenceOffset'],detectRadius=l['detectRadius'],bubbleScale=c['bubbleScale'],
  bubbleOpenDistance=c['bubbleOpenDistance'],bubbleOpenDir=c['bubbleOpenDir'],headRadius=c['headRadius']))
# TextEmoticons has an inherited-type mismatch in the typetree backend. Decode
# its complete local serialized payload instead of inventing smiley semantics.
raw=si[792].get_raw_data();pos=32
def integer():
 global pos
 value=struct.unpack_from('<i',raw,pos)[0];pos+=4;return value
def string():
 global pos
 n=integer();value=raw[pos:pos+n].decode('utf-8');pos=(pos+n+3)&~3;return value
emoticons=[]
for _ in range(integer()):
 token=string();triggers=[string() for _ in range(integer())]
 values=[dict(name=string(),value=integer()) for _ in range(integer())]
 emoticons.append(dict(emoticon=token,triggers=triggers,intVars=values))
assert pos==len(raw) and len(emoticons)==24
dot=fields(pi[25334]);dot_go=pi[dot['m_GameObject']['m_PathID']].read()
dot_transform=next(pi[c.component.m_PathID].read_typetree() for c in dot_go.m_Component if pi[c.component.m_PathID].type.name=='Transform')
out=dict(source=source,characters=chars,sprites=list(sprites.values()),stemSprite=sprite(6733),
 speakers=speakers,emoticons=emoticons,choiceArrowSprite=sprite(11075),choiceDotSprite=sprite(9430),
 choiceArrowScale=dict(x=1,y=.5,z=1),choiceDotScale=dot_transform['m_LocalScale'],
 choiceArrowInset=.16,choiceDotSpacing=.125,choiceDotYInset=.075,choiceTweenTime=.2,
 choiceAppearEvent='event:/ui/speech_bubble_choice_appear',choiceScrollEvent='event:/ui/speech_bubble_choice_scroll',
 upperCaseOnly=bool(font['upperCaseOnly']),spacingMultiplier=font['spacingMultiplier'],xPadding=font['xPadding'],
 spaceWidth=font['spaceWidth'],lineHeight=font['lineHeight'],textScale=text['scale'],width=text['width'],
 fillPadding=text['fillPadding'],letterTweenTime=text['letterTweenTime'],letterWaitTime=text['letterWaitTime'],
 spaceWaitTime=text['spaceWaitTime'],punctuationWaitTime=text['punctuationWaitTime'],timeMultiplier=text['timeMultiplier'],
 holdToSpeedUpMultiplier=text['holdToSpeedUpMultiplier'],color=text['color'],
 wobbleFPS=mesh['wobbleFPS'],wobbleSpeed=mesh['wobbleSpeed'],wobbleHeight=mesh['wobbleHeight'],
 wobbleMultiplier=mesh['wobbleMultiplier'],step=mesh['step'],
 headTrackerGameObject=si[character['headTracker']['m_PathID']].read().m_GameObject.m_PathID,
 bubbleScale=character['bubbleScale'],bubbleOpenDir=character['bubbleOpenDir'],bubbleOpenDistance=character['bubbleOpenDistance'],
 headRadius=character['headRadius'],charSoundDelay=character['charSoundDelay'],
 textEvent=character['sfxScrollText'] or 'event:/ui/dialog_textscroll',
 openEvent=character['sfxOpenSpeechBubble'] or 'event:/ui/speech_bubble_appear',
 closeEvent=character['sfxCloseSpeechBubble'] or 'event:/ui/speech_bubble_disappear',
 changeSizeEvent='event:/ui/speech_bubble_changesize',
 # CIL constants in SpeechBubble.LateUpdate, SpriteText.SetText/DoSay/Animate.
 openTime=.175,glyphFrameTime=1/6,changeFadeMin=.05,changeFadeTime=.1,changeScrollDelay=.075,resizeTime=.15,
 closeTextDelay=.0625,closeFadeMin=.1575,closeFadeMax=.175,
 sortingLayer=-1016395719,glyphOrder=10,meshOrder=-10,stemOrder=0,
 scope='Main speech bubble and Janitor/choice presentation; original artwork stays local.')
# sprite(6733) was appended while evaluating the dictionary.
out['sprites']=list(sprites.values())
(DEST/'Presentation.json').write_text(json.dumps(out,indent=2)+'\n')
print('Recovered station dialogue:',len(chars),'glyph definitions,',len(sprites),'sprites; headTracker',out['headTrackerGameObject'])
