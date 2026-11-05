"""Regression cases for accidental artifact publication and stale manifests."""
import hashlib
import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('publication_audit',ROOT/'scripts/check_publication.py')
guard=importlib.util.module_from_spec(spec);spec.loader.exec_module(guard)

class PublicationTests(unittest.TestCase):
 def setUp(self):self.files=guard.load(ROOT)
 def test_reviewed_snapshot_passes(self):self.assertEqual(guard.audit(self.files),[])
 def test_unlisted_asset_is_rejected(self):
  self.files['NITW-Unity-Reconstruction/Assets/Resources/image.png']=b'fake image'
  self.assertTrue(any('Forbidden path/type' in e for e in guard.audit(self.files)))
 def test_binary_disguised_as_source_is_rejected(self):
  p='Tools/inspect_game_window.py';self.files[p]+=b'\0'
  self.assertTrue(any('Binary/NUL' in e for e in guard.audit(self.files)))
 def test_changed_source_requires_review(self):
  self.files['Tools/inspect_game_window.py']+=b'\n# unreviewed modification\n'
  self.assertTrue(any('Changed bytes' in e for e in guard.audit(self.files)))
 def test_manifest_cannot_admit_known_runtime_port(self):
  p='NITW-Unity-Reconstruction/Assets/Scripts/RecoveredStationPlayer.cs'
  self.files[p]=b'// deliberately withheld runtime port\n'
  m=json.loads(self.files[guard.MANIFEST]);m['files'].append({'path':p,'sha256':hashlib.sha256(self.files[p]).hexdigest()})
  self.files[guard.MANIFEST]=json.dumps(m).encode()
  self.assertTrue(any('Forbidden path/type' in e for e in guard.audit(self.files)))
 def test_commit_audit_uses_committed_bytes(self):
  with tempfile.TemporaryDirectory() as tmp:
   root=pathlib.Path(tmp)
   def git(*args):return subprocess.check_output(['git','-C',tmp,*args],stderr=subprocess.DEVNULL)
   git('init','-q')
   for path,raw in self.files.items():
    p=root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(raw)
   git('add','-f','.')
   git('-c','user.name=Audit Test','-c','user.email=audit@example.invalid','commit','-qm','fixture')
   (root/'README.md').write_text('uncommitted change')
   self.assertEqual(guard.audit(guard.load(root,'HEAD')),[])
   self.assertTrue(any('Changed bytes' in e for e in guard.audit(guard.load(root))))

if __name__=='__main__':unittest.main()
