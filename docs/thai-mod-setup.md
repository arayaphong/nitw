# Thai mod: เตรียมเครื่องและรัน

คู่มือนี้เริ่มจาก clone repository (branch `thai-mod`) จนเปิดเกมพร้อมภาษาไทย ทุกคำสั่งรันจากโฟลเดอร์ราก repository เว้นแต่ระบุไว้

ภาพรวม: ไม่มีการแก้ไฟล์เกม mod เพิ่มไฟล์ 3 อย่างในโฟลเดอร์เกม (`NITWThai/`, `doorstop_libs/`, `run_nitwthai.sh`) และถอดออกได้หมด

## 1. สิ่งที่ต้องมี

| สิ่งที่ต้องมี | ใช้ทำอะไร | หมายเหตุ |
|---|---|---|
| Linux + Night in the Woods (Steam, รุ่น Linux) | ตัวเกม | ทดสอบกับ Unity 5.6.2p4, Weird Autumn Edition |
| Python 3 | เครื่องมือ offline | |
| ไลบรารีระบบ ICU (`libicuuc.so`) | ตัดคำภาษาไทย | Arch/CachyOS: `icu`, Debian/Ubuntu: `libicu-dev` |
| `curl`, `unzip`, `sha256sum` | `fetch_deps.sh` | |
| Unity editor ที่มี `Data/NetCoreRuntime/dotnet` และ `Data/DotNetSdkRoslyn/csc.dll` | คอมไพล์ mod (net35) | ใช้แค่ตัวคอมไพเลอร์; ทดสอบกับ 6000.3.28f1 |
| คำแปล (`ThaiMod/translations/`) | ข้อความไทย | **ไม่อยู่ใน repository** (เป็นข้อความในเกม) ต้องมีสำเนาของตัวเอง ดูข้อ 5 |

ตัวแปรสภาพแวดล้อม (ตั้งเมื่อไม่ได้ใช้ค่าเริ่มต้น):

| ตัวแปร | ค่าเริ่มต้น |
|---|---|
| `NITW_GAME_DATA` | `~/.local/share/Steam/steamapps/common/Night in the Woods/Night in the Woods_Data` |
| `UNITY_EDITOR` | `~/Unity/Hub/Editor/6000.3.28f1/Editor` |
| `NITW_THAI_FONT`, `NITW_THAI_POEM_FONT` | ฟอนต์ใน `ThaiMod/fonts/` ที่ `fetch_deps.sh` ดาวน์โหลด |
| `NITW_PRESENTATION` | `NITW-Unity-Reconstruction/Assets/Resources/RecoveredStationDialogue/Presentation.json` |

## 2. Python environment

```sh
python3 -m venv .tools/unity-inspect
.tools/unity-inspect/bin/pip install -r requirements-tools.txt
```

## 3. ของจากภายนอก (BepInEx 5.4.22 และฟอนต์)

```sh
ThaiMod/fetch_deps.sh
```

ดาวน์โหลด `BepInEx_unix_5.4.22.0.zip` จาก GitHub ของ BepInEx (ตรวจ SHA-256) แล้วสร้าง:
- `ThaiMod/lib/doorstop_libs/` (UnityDoorstop 3) และ `ThaiMod/lib/Mono.Cecil.dll`
- `ThaiMod/run_nitwthai.sh` — ดัดแปลงจาก `run_bepinex.sh` ให้เรียก `NITWThai/NITWThai.Loader.dll`
- `ThaiMod/fonts/` — Kanit Medium และ Noto Serif Thai จาก google/fonts (SIL OFL)

> ใช้ Noto Serif Thai จาก google/fonts เท่านั้น: รุ่นจาก notofonts ไม่มีตัวเลข/ตัวละติน ซึ่งบทกวีใช้ (เช่น "1998")

## 4. ข้อมูลจากเกม (อ่านอย่างเดียว เก็บในเครื่อง)

```sh
.tools/unity-inspect/bin/python -I Tools/extract_station_dialogue.py   # metric ฟอนต์ SpriteText + ตาราง emoticon -> Presentation.json
.tools/unity-inspect/bin/python -I Tools/extract_game_yarn.py          # Yarn ทั้งเกม -> ThaiMod/source/yarn/
```

## 5. ไฟล์งานแปล

```sh
python3 -I Tools/extract_thai_worklist.py
```

สร้าง/อัปเดต `ThaiMod/translations/<yarn>.json` หนึ่งไฟล์ต่อ Yarn (ช่อง `th` ว่างถ้ายังไม่แปล) พร้อม `INDEX.md` รันซ้ำได้ ไม่ทับคำแปลเดิม

- ถ้ามีคำแปลอยู่แล้ว ให้คัดลอกโฟลเดอร์ `ThaiMod/translations/` ของตัวเองมาวางก่อนรันคำสั่งนี้
- **สำรอง `ThaiMod/translations/` สม่ำเสมอ** เพราะไม่อยู่ใน repository
- กติกาตัดบรรทัดอยู่ใน `translations/keep_together.json` และ `translations/no_break_after.json`

## 6. Build

```sh
.tools/unity-inspect/bin/python -I Tools/prepare_thai_mod.py      # ข้อมูลไทย -> ThaiMod/data/
ThaiMod/build.sh "$HOME/.local/share/Steam/steamapps/common/Night in the Woods"   # DLL -> ThaiMod/out/
```

- `prepare_thai_mod.py` สรุปท้ายว่ามีบรรทัดที่ถูกข้าม (`errors`) หรือยาวเกิน 3 แถว (`overflows`) กี่บรรทัด รายละเอียดอยู่ใน `ThaiMod/reports/thai_build.json`; บรรทัดที่ถูกข้ามจะแสดงภาษาอังกฤษในเกม
- `build.sh` อ่าน DLL ของเกมเป็น reference เท่านั้น ไม่เขียนอะไรลงโฟลเดอร์เกม

## 7. ติดตั้งและเปิดเกม

```sh
ThaiMod/install.sh install "$HOME/.local/share/Steam/steamapps/common/Night in the Woods"
```

`install.sh` เพิ่มเฉพาะ `NITWThai/` (DLL + `data/`), `doorstop_libs/`, `run_nitwthai.sh` และไม่ยอมเขียนทับไฟล์ที่ไม่ได้ติดตั้งเอง รันซ้ำเพื่ออัปเดตได้

เปิดเกม (เลือกอย่างใดอย่างหนึ่ง):
- ตรงจากโฟลเดอร์เกม: `"<โฟลเดอร์เกม>/run_nitwthai.sh"` (ไม่ต้องเปิด Steam)
- ผ่าน Steam: Properties → General → Launch Options: `./run_nitwthai.sh %command%` (ยังไม่ได้ทดสอบ)

> การเปิดไฟล์ `Night in the Woods.x86_64` ตรงๆ หรือกด Play ใน Steam โดยไม่มี launch option จะได้เกมเดิม (ไม่มี mod)

**สำรองเซฟก่อนทดสอบ:** `~/.config/unity3d/Infinite Fall/Night in the Woods/` (Start New Game ลบเซฟเดิมตามปกติของเกม) หรือทดสอบด้วยโปรไฟล์แยก: `XDG_CONFIG_HOME=/path/to/test-profile "<โฟลเดอร์เกม>/run_nitwthai.sh"`

## 8. ตรวจว่าทำงาน

- `<โฟลเดอร์เกม>/NITWThai/loader.log` — ต้องมี `hooked ...` 3 บรรทัด (`GetString`, `GetCharData`, `SetText`) และ `patched Assembly-CSharp loaded`; ถ้ามี `FAILED` เกมจะรันแบบไม่มี mod
- `<โฟลเดอร์เกม>/NITWThai/trace.log` — `thai data: N strings ...`, key ที่เกมดึง และข้อความที่วาด
- หน้า Title ต้องกดยืนยันได้ (C/Space)
- `python3 -I Tools/thai_coverage.py` — บรรทัดที่เจอในเกมแต่ยังไม่แปล (`ThaiMod/reports/coverage.md`)

สวิตช์สำหรับวินิจฉัย (ใส่หน้า `run_nitwthai.sh`):

| ตัวแปร | ผล |
|---|---|
| `NITWTHAI_LANG=en` | ปิดภาษาไทย (เกมเป็นอังกฤษ แต่ยังโหลด mod) |
| `NITWTHAI_MODE=off` | Doorstop ทำงานแต่ไม่ patch |
| `NITWTHAI_MODE=roundtrip` | patch แบบไม่ใส่ hook |
| `NITWTHAI_HOOKS=GetString,SetText` | ใส่เฉพาะ hook ที่ระบุ |
| `NITWTHAI_DUMP=1` | เขียน `NITWThai/Assembly-CSharp.patched.dll` ไว้ตรวจ |

## 9. ถอดออก

```sh
ThaiMod/install.sh uninstall "$HOME/.local/share/Steam/steamapps/common/Night in the Woods"
```

ลบเฉพาะสิ่งที่ `install.sh` ติดตั้ง (รวม log ใน `NITWThai/`) และลบ launch option ใน Steam ถ้าตั้งไว้ โฟลเดอร์เกมจะกลับเหมือนเดิม (ตรวจด้วย `diff -r` แล้วในเฟส 0)

## 10. วงจรแปลประจำวัน

1. แก้ช่อง `th` ใน `ThaiMod/translations/<yarn>.json` (คง emoticon/markup เช่น `:)`, `-_-`, `{width=4}` ไว้เหมือนต้นฉบับ)
2. `.tools/unity-inspect/bin/python -I Tools/prepare_thai_mod.py` แล้วดู errors/overflows
3. `ThaiMod/install.sh install "<โฟลเดอร์เกม>"` (ปิดเกมก่อน)
4. เล่นทดสอบ แล้ว `python3 -I Tools/thai_coverage.py` หาบรรทัดถัดไปที่ควรแปล
