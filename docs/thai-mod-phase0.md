# Thai mod เฟส 0: ผลการวางฐาน mod

ปรับปรุง 6 พฤศจิกายน 2026 · branch `thai-mod`

## สรุป

- **BepInEx 5 ใช้กับเกมนี้ไม่ได้** ทดสอบ 5.4.17, 5.4.21, 5.4.22 และ 5.4.23.5 ทุกตัวล้มใน preloader ที่จุดเดียวกัน: `HarmonyException: IL Compile Error` → `NullReferenceException` ใน `MonoMod.RuntimeDetour.DetourHelper.GetIdentifiable` ตอนที่ BepInEx ติดตั้ง Harmony patch ของตัวเอง ลอง `HarmonyBackend` ครบทั้ง `auto`, `cecil`, `dynamicmethod`, `methodbuilder` และ entrypoint `Application`/`Camera`/`MonoBehaviour` แล้ว ผลเหมือนกัน สรุปว่า MonoMod ทำ runtime detour บน Mono ของ Unity `5.6.2p4` ไม่ได้ จึงใช้ Harmony ไม่ได้ไม่ว่าจะผ่าน BepInEx หรือไม่
- **แนวทางใหม่ที่ใช้ได้: UnityDoorstop + Mono.Cecil patch ใน memory** loader ของเราถูก Doorstop เรียกทันทีหลัง Mono เริ่มทำงาน ก่อน Unity โหลด assembly ของเกม loader อ่าน `Assembly-CSharp.dll` ด้วย Cecil, แทรก hook แล้วโหลด image ที่ patch แล้วเข้า domain ก่อน เมื่อ Unity โหลด `Assembly-CSharp` ตามชื่อจะได้ตัวที่ patch แล้ว **ไฟล์เกมบนดิสก์ไม่ถูกแก้เลย**
- **พิสูจน์แล้วว่าเกมรันโค้ดที่ patch:** hook บน `Global.LoadLevel` ทำงานตอนเปลี่ยนจาก logos ไปหน้า Title (`scene Title` ใน trace) โดยไม่ต้องกดปุ่มใดๆ (hook นี้ถูกถอดออกภายหลัง ดูหัวข้อ "ปัญหาปุ่มยืนยัน")
- **เล่นจริงผ่านแล้ว (ผู้ใช้ทดสอบจากโฟลเดอร์ Steam ด้วย `run_nitwthai.sh`):** Title → BusStation → คุยกับ Janitor, เซฟและ Continue ได้ปกติ, `trace.log` บันทึก line ID คู่กับข้อความครบ (เช่น `line:f53579` → บรรทัดแรกของ EnterLevel) ตรงกับ line ID ที่ใช้ในไฟล์คำแปลไทย
- **ถอดออกได้สะอาด:** `install.sh install` แล้ว `install.sh uninstall` บนสำเนาของเกม ผล `diff -r` เทียบกับโฟลเดอร์ Steam ต้นฉบับ: เหมือนกันทุกไฟล์
- แนวทางนี้เข้ากับเป้าหมาย Android ด้วย: โค้ด Cecil ชุดเดียวกันสร้าง `Assembly-CSharp.dll` ที่ patch แล้วเป็นไฟล์สำหรับ re-host ได้ ซึ่ง Harmony แบบ runtime ทำไม่ได้บน IL2CPP อยู่แล้ว

## รายละเอียดทางเทคนิค

### Doorstop

- ใช้ `doorstop_libs` (UnityDoorstop 3) จากแพ็กเกจ `BepInEx_unix_5.4.22.0.zip` และสคริปต์เปิดเกมที่ดัดแปลงจาก `run_bepinex.sh` ของแพ็กเกจเดียวกัน (`DOORSTOP_INVOKE_DLL_PATH` ชี้ไปที่ `NITWThai/NITWThai.Loader.dll`)
- Doorstop 3 หา entrypoint ด้วย `*:Main` คือ method `static void Main()` ในคลาสใดก็ได้ ไม่ใช่ `Doorstop.Entrypoint.Start()` แบบ Doorstop 4; ถ้าชื่อผิด log ของ Mono จะขึ้น `Failed to locate any entrypoints!`
- loader อ่าน path ของตัวเองจาก `DOORSTOP_INVOKE_DLL_PATH` เป็นหลัก (สำรองด้วย `Assembly.Location`)

### hook ของเฟส 0 (tracer)

loader แทรก `ldarg <พารามิเตอร์แรก>; call NITWThai.Runtime.Hooks::OnX(string)` ที่ต้น method; บันทึกลง `NITWThai/trace.log` (เวลา, ชนิด, ค่า):

| method ในเกม | hook | บันทึก |
|---|---|---|
| `Yarn.Program::GetString(string key)` | `OnGetString` | line ID/คีย์สตริงที่ VM ขอ (ครั้งแรกต่อ session) |
| `SpriteText::SetText(string text, bool popIn, bool sfxEnabled)` | `OnSetText` | ข้อความที่ถูกวาดจริงในบับเบิล/UI ที่ใช้ SpriteText |
| ~~`Global::LoadLevel(string sceneName, ...)`~~ | ~~`OnLoadLevel`~~ | ถอดออกแล้ว (ดูด้านล่าง) |

ทุก spec ต้อง match อย่างน้อยหนึ่ง overload ไม่งั้น loader ยกเลิกการ patch ทั้งหมดและเกมรันแบบเดิม (เขียนสาเหตุลง `NITWThai/loader.log`)

สวิตช์สำหรับวินิจฉัย (ตัวแปรสภาพแวดล้อม):
- `NITWTHAI_MODE=off` — Doorstop เรียก loader แต่ไม่ patch; `roundtrip` — Cecil อ่าน/เขียน `Assembly-CSharp` กลับโดยไม่ใส่ hook; ไม่ตั้ง = ใส่ hook ตามปกติ
- `NITWTHAI_HOOKS=GetString,SetText` — เลือกเฉพาะ hook ตามชื่อ method

### ปัญหาปุ่มยืนยัน (แก้แล้ว)

ตอนมี hook ครบ เมนู Title เลื่อนขึ้นลงได้ แต่กดยืนยันไม่ได้ (Enter/Space/C) เมนูอ่านทิศทางจาก axis ของ Rewired (`Global.UpdateInput` → `Vertical`) แต่อ่านปุ่มยืนยันจาก `Global.IsConfirmOrInteractDown` → `Rewired.Player.GetButtonDown("Interact"/"Jump")` ผลแยกทดสอบโดยผู้ใช้:

| การตั้งค่า | กดยืนยัน |
|---|---|
| ไม่มี mod | ได้ |
| `NITWTHAI_MODE=off` | ได้ |
| `NITWTHAI_MODE=roundtrip` | ได้ |
| `NITWTHAI_HOOKS=GetString,SetText` | ได้ |
| hook ครบ (รวม `Global.LoadLevel`) | **ไม่ได้** |

จึงถอด hook `Global.LoadLevel` ออก (เป็น hook สำหรับวินิจฉัยเท่านั้น) สาเหตุเชิงกลไกยังไม่ทราบ ข้อควรระวังสำหรับเฟสถัดไป: hook ใหม่ทุกตัวต้องทดสอบปุ่มยืนยันที่ Title ด้วย

### เกมกับ Steam และการทดสอบอัตโนมัติ

- รันจากโฟลเดอร์โดยตรงได้โดยไม่ต้องมี Steam: `SteamManager.Awake` เรียก `RestartAppIfNecessary(AppId_t.Invalid)` ซึ่งคืน false และถ้า `SteamAPI.Init()` ล้มเกมแค่ log error แล้วเล่นต่อ
- ทดสอบด้วย `XDG_CONFIG_HOME` แยก เพื่อไม่ให้แตะเซฟจริง (เซฟ/prefs ของเกมอยู่ใต้ `unity3d/Infinite Fall/Night in the Woods/`) และสำรองเซฟจริงไว้ก่อนเริ่ม
- Unity หยุดอัปเดตเมื่อหน้าต่างเกมไม่มี focus; logos ใช้เวลาราว 40 วินาทีของเวลาที่มี focus
- **กดปุ่มอัตโนมัติไม่ได้:** XTest ส่ง key ถึงหน้าต่างเกม (XWayland) ได้ แต่ปุ่ม Rewired (`Interact`/`Jump`) ไม่รับ key จาก XTest แม้ในเกมที่ไม่มี mod (ทดสอบ C ในเกมเดิมแล้วไม่ผ่าน) การทดสอบที่ต้องกดยืนยันจึงต้องให้คนเล่น หรือทำตัวช่วยทดสอบภายใน mod ในเฟสถัดไป

### ข้อมูลสำหรับเฟส 1 (จาก CIL)

- `Localization.useLanguage` เป็น `UnityEngine.SystemLanguage` (`English` = 10, `Thai` = 36); `InitDialogueRunner` ตั้ง `DialogueRunner.shouldOverrideLanguage`/`overrideLanguage` เมื่อไม่ใช่ English และส่ง `stringGroups` ให้
- `DialogueRunner.Start` และ `AddScript` หา `LocalisedStringGroup` ที่ `language` ตรงกับ `overrideLanguage` แล้วเรียก `AddStringTable(TextAsset.text)` ทีละไฟล์ (CSV `LineCode,LineText,Comment`)
- `Yarn.Dialogue.AddStringTable(stringTable)` รับ table ที่ parse แล้ว: เป็นทางเลือกสำหรับใส่คำแปลไทยจาก mod โดยตรงโดยไม่ต้องสร้าง `TextAsset`

## โครงสร้างงาน (ในเครื่อง)

```
ThaiMod/
  src/Loader/Entrypoint.cs   loader: Doorstop entry + Cecil patch
  src/Runtime/Hooks.cs       hook ที่เกมเรียก (เฟส 0: tracer)
  build.sh                   คอมไพล์ net35 ด้วย Roslyn ที่มากับ Unity editor
  install.sh                 install/uninstall ในโฟลเดอร์เกมใดก็ได้ (เพิ่มไฟล์อย่างเดียว)
  run_nitwthai.sh            สคริปต์เปิดเกมผ่าน Doorstop
  lib/                       Mono.Cecil.dll, doorstop_libs (จาก BepInEx 5.4.22)
  tools/cil_dump.py          อ่าน CIL ของ method แบบ static
  game/                      สำเนาเกมแบบ reflink สำหรับพัฒนา
```

## สถานะเฟส 0

- [x] เกมเปิดพร้อม mod ได้จากโฟลเดอร์ Steam (`run_nitwthai.sh`, ติดตั้งด้วย `install.sh` เมื่อ 6 พ.ย. 2026)
- [x] เล่นถึงบทสนทนา Janitor, เซฟ และ Continue ได้ปกติ
- [x] `trace.log` มี line ID และข้อความ
- [x] ถอด mod ออกแล้วโฟลเดอร์เกมเหมือนต้นฉบับ (`diff -r`)
- [ ] เปิดผ่าน Steam client ด้วย launch option `./run_nitwthai.sh %command%` (ยังไม่ได้ทดสอบ; ทดสอบเมื่อเปิด Steam)
