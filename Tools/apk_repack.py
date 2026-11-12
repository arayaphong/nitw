#!/usr/bin/env python3
"""Rebuild the Android APK with replaced entries, then zipalign and sign it.

  apk_repack.py <original.apk> <out.apk> [entry=file ...]

Every entry keeps its original compression method. Always applied:
  * lib/arm64-v8a/libfmod.so: the SHA1 CPU-feature check is skipped
    (file offset 0xbeacc, `tbnz w0,#4` -> `b`), because the emulator's ARM
    translation reports no SHA1 and FMOD then fails with ERR_NEEDSHARDWARE.
  * the old META-INF signature files are dropped; the result is signed with
    the Android debug keystore.
"""
import pathlib, shutil, subprocess, sys, zipfile
from os import getenv

FMOD = 'lib/arm64-v8a/libfmod.so'
FMOD_OFF, FMOD_OLD, FMOD_NEW = 0xbeacc, bytes.fromhex('60002037'), bytes.fromhex('03000014')
DROP = {'META-INF/CERT.RSA', 'META-INF/CERT.SF', 'META-INF/MANIFEST.MF'}
SDK = pathlib.Path(getenv('ANDROID_HOME', pathlib.Path.home() / 'Android/Sdk'))
KEYSTORE = pathlib.Path.home() / '.android/debug.keystore'


def main(src, dst, pairs):
    repl = dict(p.split('=', 1) for p in pairs)
    dst = pathlib.Path(dst); dst.parent.mkdir(parents=True, exist_ok=True)
    raw, aligned = dst.with_suffix('.unsigned.apk'), dst.with_suffix('.aligned.apk')
    used = set()
    with zipfile.ZipFile(src) as zi, zipfile.ZipFile(raw, 'w', allowZip64=True) as zo:
        for info in zi.infolist():
            name = info.filename
            if name in DROP:
                continue
            ni = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            ni.compress_type, ni.external_attr = info.compress_type, info.external_attr
            if name == FMOD:
                b = bytearray(zi.read(info))
                assert b[FMOD_OFF:FMOD_OFF + 4] == FMOD_OLD, 'unexpected libfmod.so'
                b[FMOD_OFF:FMOD_OFF + 4] = FMOD_NEW
                zo.writestr(ni, bytes(b))
            elif name in repl:
                with open(repl[name], 'rb') as f, zo.open(ni, 'w', force_zip64=True) as g:
                    shutil.copyfileobj(f, g, 1 << 22)
                used.add(name)
            else:
                with zi.open(info) as f, zo.open(ni, 'w', force_zip64=info.file_size > 0x7fffffff) as g:
                    shutil.copyfileobj(f, g, 1 << 22)
    assert used == set(repl), f'entries not in the APK: {set(repl) - used}'
    tools = sorted((SDK / 'build-tools').iterdir())[-1]
    subprocess.run([tools / 'zipalign', '-f', '-P', '16', '4', raw, aligned], check=True)
    subprocess.run([tools / 'apksigner', 'sign', '--ks', KEYSTORE, '--ks-pass', 'pass:android',
                    '--ks-key-alias', 'androiddebugkey', '--key-pass', 'pass:android',
                    '--out', dst, aligned], check=True, stderr=subprocess.DEVNULL)
    subprocess.run([tools / 'apksigner', 'verify', dst], check=True, stderr=subprocess.DEVNULL)
    raw.unlink(); aligned.unlink()
    print('signed', dst, dst.stat().st_size)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
