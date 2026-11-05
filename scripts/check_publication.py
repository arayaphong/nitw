#!/usr/bin/env python3
"""Audit an allowlisted source snapshot, or the exact bytes in a Git revision."""
import argparse
import ast
import hashlib
import json
import pathlib
import re
import subprocess
import sys

MANIFEST='publication-manifest.json'
ROOT_FILES={'README.md','PUBLICATION.md','LICENSE','.gitignore','requirements-tools.txt',MANIFEST,'.githooks/pre-push'}
RUNTIME_NAMES={
 'RecoveredFireflyNative.cs','RecoveredPoemThai.cs','RecoveredOptionsData.cs','RecoveredPoemData.cs',
 'RecoveredStationData.cs','RecoveredStationMovementData.cs','RecoveredTitleData.cs','RecoveredTitleMenuData.cs',
 'RecoveredFireflyAudioCheck.cs','RecoveredIntroRuntimeCheck.cs','RecoveredMurderflyRuntimeCheck.cs',
 'RecoveredPoemCheck.cs','RecoveredStationCheck.cs','RecoveredStationPlayerCheck.cs','RecoveredTitleMenuCheck.cs',
 'RecoveredTitleRuntimeCheck.cs','RecoveredStationNarrationCheck.cs','RecoveredStationDialogueData.cs'}
EDITOR_NAMES={'CreateReferenceScene.cs','DiagnoseIntroColor.cs','RecoveredIntroImporter.cs','RecoveredOptionsImporter.cs',
 'RecoveredPoemImporter.cs','RecoveredStationAnimationImporter.cs','RecoveredStationImporter.cs',
 'RecoveredTitleImporter.cs','RecoveredTitleMenuImporter.cs','VerifyRecoveredOptions.cs',
 'RecoveredStationDialogueImporter.cs','RecoveredStationJanitorImporter.cs'}

def eligible(path):
 p=pathlib.PurePosixPath(path)
 if p.is_absolute() or '..' in p.parts or str(p)!=path:return False
 if path in ROOT_FILES:return True
 if path.startswith('NITW-Unity-Reconstruction/Assets/Scripts/'):
  return len(p.parts)==4 and p.name in RUNTIME_NAMES
 if path.startswith('NITW-Unity-Reconstruction/Assets/Editor/'):
  return len(p.parts)==4 and p.name in EDITOR_NAMES
 return p.suffix=='.py' and (len(p.parts)==2 and p.parts[0] in {'Tools','scripts','tests'}
  or path=='NITW-Unity-Reconstruction/Tools/extract_intro_scene.py')

def audit(files):
 """Return errors; `files` maps repository-relative paths to byte strings."""
 errors=[]
 try:
  manifest=json.loads(files[MANIFEST].decode('utf-8'))
  if manifest['format']!=1:raise ValueError('unsupported manifest version')
  entries=manifest['files'];allowed={row['path']:row for row in entries}
  if len(allowed)!=len(entries):raise ValueError('duplicate manifest paths')
 except (KeyError,ValueError,UnicodeError,TypeError) as e:return ['Invalid/missing publication manifest: '+str(e)]
 expected=set(allowed)|{MANIFEST}
 for path in sorted(set(files)-expected):errors.append('Unreviewed path: '+path)
 for path in sorted(expected-set(files)):errors.append('Missing reviewed path: '+path)
 for path,raw in sorted(files.items()):
  if not eligible(path):errors.append('Forbidden path/type: '+path)
  if len(raw)>128*1024:errors.append('Oversized source: '+path)
  if b'\0' in raw:errors.append('Binary/NUL content: '+path)
  try:text=raw.decode('utf-8')
  except UnicodeError:
   errors.append('Non-UTF-8 content: '+path);continue
  if re.search(r'/(?:home|Users)/[A-Za-z0-9][A-Za-z0-9_.-]*/',text):errors.append('Machine-specific home path: '+path)
  if re.search(r'(?m)^\s*IL_[0-9a-fA-F]{4,}:\s+\w+',text):errors.append('CIL dump: '+path)
  if re.search(r'-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----',text):errors.append('Private key material: '+path)
  if re.search(r'[A-Za-z0-9+/=_-]{512,}',text):errors.append('Potential encoded payload: '+path)
  if path.endswith('.py'):
   try:ast.parse(text,filename=path)
   except SyntaxError as e:errors.append('Python syntax: '+str(e))
  if path!=MANIFEST and path in allowed and hashlib.sha256(raw).hexdigest()!=allowed[path].get('sha256'):
   errors.append('Changed bytes require review: '+path)
 return errors

def load(root,revision=None):
 if revision:
  def git(*args):return subprocess.check_output(['git','-C',str(root),*args])
  names=git('ls-tree','-r','--name-only','-z',revision).decode().split('\0')
  return {p:git('show',revision+':'+p) for p in names if p}
 return {p.relative_to(root).as_posix():p.read_bytes() for p in root.rglob('*') if p.is_file()
  and not any(part in {'.git','__pycache__'} for part in p.relative_to(root).parts)}

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--root',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[1])
 parser.add_argument('--revision',help='Inspect committed blobs, e.g. HEAD; do not substitute working-tree files')
 args=parser.parse_args()
 try:files=load(args.root.resolve(),args.revision);errors=audit(files)
 except (OSError,subprocess.CalledProcessError) as e:print('AUDIT FAILED:',e,file=sys.stderr);return 1
 if errors:
  print('AUDIT FAILED:\n'+'\n'.join(errors),file=sys.stderr);return 1
 print('SOURCE AUDIT PASS:',len(files),'reviewed UTF-8 text files;',sum(map(len,files.values())),'bytes')
 return 0

if __name__=='__main__':sys.exit(main())
