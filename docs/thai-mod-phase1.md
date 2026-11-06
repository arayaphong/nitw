# Thai mod เฟส 1: ภาษาไทยในเกมจริง (vertical slice)

ปรับปรุง 6 พฤศจิกายน 2026 · branch `thai-mod`

## ผล

เล่นในเกม Steam จริง (Linux, เปิดด้วย `run_nitwthai.sh`) ผ่านแล้ว:

- หน้า Title กดยืนยันได้ → Start New Game
- **IntroPoem เป็นภาษาไทยครบ 36 บรรทัด** (รวมตัวเลือก) ระยะบรรทัด ย่อหน้า และสองคอลัมน์ตรงกับต้นฉบับ
- **BusStation เป็นภาษาไทยครบ 31 บรรทัด**: บท EnterLevel, บทคุยกับ Janitor ทุกโหนด และตัวเลือก 2 ข้อ; อีโมติคอน (`:)`, `-_-`) และ `{width=4}` ยังทำงานตามเดิม
- ข้อความที่ยังไม่แปล (ทีวี, ป้าย ฯลฯ) แสดงภาษาอังกฤษตามปกติ
- `NITWTHAI_LANG=en` ปิดภาษาไทย (เกมกลับเป็นอังกฤษทั้งหมด)

## หลักการ: ให้เกมวาดภาษาไทยด้วยระบบของมันเอง

ไม่เขียนตัววาดข้อความใหม่ แต่ทำให้ภาษาไทยดูเหมือน "ตัวอักษรเพิ่มเติม" ของฟอนต์ที่เกมมีอยู่:

1. **ข้อความ:** hook `Yarn.Program.GetString(key)` (override) คืนบรรทัดไทยตาม `line:<id>` ถ้ามี ไม่งั้นคืนต้นฉบับ
2. **กลุ่มอักษร = หนึ่งตัวอักษร:** พยัญชนะ + สระ + วรรณยุกต์แต่ละกลุ่มถูก shape ล่วงหน้าด้วย HarfBuzz แล้วแทนด้วยอักขระ Private Use Area หนึ่งตัว (เริ่ม U+E000)
3. **บับเบิล (SpriteText):** hook `SpriteFont.GetCharData(char)` (override) คืน `CharData` ที่มี sprite ของกลุ่มอักษรนั้น (Kanit Medium) เกมจึงจัดบรรทัด พิมพ์ทีละตัว เด้ง fade และเล่นเสียงตัวอักษรเองทั้งหมด
4. **การตัดบรรทัด:** `SpriteText.ParseText` ตัดบรรทัดเฉพาะที่ช่องว่าง จึงตัดล่วงหน้าแบบ offline ด้วยตำแหน่งตัดคำของ ICU + รายการคำห้ามตัด และ metric ความกว้างเดียวกับเกม (`GetWidth × spacingMultiplier × scale + xPadding`, กว้าง 3 / `{width}` / 4.5 เมื่อเกิน 3 บรรทัด) แล้วใส่ `|` (ขึ้นบรรทัดใหม่ของเกม); ช่องว่างในข้อความไทยเปลี่ยนเป็นอักขระเว้นวรรคที่ตัดไม่ได้ เพื่อไม่ให้เกมตัดซ้ำ
5. **ระยะบรรทัดบับเบิล:** ครั้งแรกที่ `SpriteFont` วาดภาษาไทย เพิ่ม `lineHeight` เป็น 1.43 (ระยะ 0.5 หน่วย) ให้พอสำหรับสระ/วรรณยุกต์ซ้อน
6. **ชื่อผู้พูด:** ใน Yarn ของเกมชื่อผู้พูดอยู่ในข้อความ (`Mae: ...`, `Janitor:{width=4} ...`); ไปป์ไลน์คัดลอก prefix นี้จากไฟล์ `.yarn` ต้นฉบับตาม line ID ทุกตัวอักษร
7. **บทกวี (TextMesh):** บทกวีพิมพ์ทีละตัวลง `TextMesh` ที่ clone จาก `DialogueImplementation.prefabTextMeshLine` จึงสร้าง Unity custom `Font` (Noto Serif Thai 64px, ตัวอักษร PUA ชุดเดียวกัน + ASCII/เครื่องหมายคำพูด) แล้วสลับฟอนต์ของ prefab ทันทีที่ `GetString` ดึงบรรทัดบทกวี (เกมดึงทั้งโหนดตอนเริ่ม จึงเกิดก่อนบรรทัดแรก)
8. **ระยะบรรทัดบทกวี:** เกมวางบรรทัดที่ `poemStarts[col] + poemColumnLines × (ความสูง bounds ของ clone ที่ยังว่าง)`; ฟอนต์ custom ที่สร้างตอนรันมี line spacing 0 และ Unity 5.6 ตั้งค่านี้ไม่ได้ (ลองตัวอักษร "strut" ที่สูงเท่าบรรทัดแล้วไม่ได้ผล) จึงใส่ component `PoemLineFix` ให้ prefab: `Awake` (ขณะ Instantiate ก่อนเกมตั้งตำแหน่ง) อ่าน `poemColumnLines`, `Start` (ก่อนวาดครั้งแรก) เลื่อนลง `lines × 0.29464` ซึ่งคือความสูงบรรทัดของฟอนต์ Goudy เดิม

## hook ที่ใช้ (ชุดที่ปลอดภัย)

| method ในเกม | ชนิด | หน้าที่ |
|---|---|---|
| `Yarn.Program::GetString(string)` | override | คืนบรรทัดไทย; สลับฟอนต์บทกวี |
| `SpriteFont::GetCharData(char)` | override | `CharData` ของกลุ่มอักษรไทย |
| `SpriteText::SetText(string, bool, bool)` | observe | trace ข้อความที่วาด |

**ข้อควรระวัง:** การ patch `Global.LoadLevel` (เฟส 0) หรือ `SpeechBubble.Say` + `DialogueImplementation.ShowPoemLine` (เฟส 1) ทำให้ปุ่มยืนยันที่หน้า Title ใช้ไม่ได้ ทั้งที่ hook ยังไม่ทันทำงาน (trace ว่าง) ส่วน axes ยังใช้ได้ สาเหตุยังไม่ทราบ ชุด 3 hook ข้างบนผ่านทุกครั้ง; ทุกครั้งที่เพิ่ม hook ต้องทดสอบปุ่มยืนยันที่ Title

## ข้อมูลและไฟล์

- `Tools/prepare_thai_mod.py` — ไปป์ไลน์ offline: อ่านไฟล์คำแปล (keyed by line ID), `.yarn` ต้นฉบับ (prefix ผู้พูด) และ metric ของ SpriteText ที่สกัดไว้ แล้วสร้าง `ThaiMod/data/` ได้แก่ `strings.tsv` (`line:<id>`, ข้อความแสดงผล, `bubble`/`poem`), `glyphs.png/tsv` (atlas Kanit + pivot + ความกว้าง `CharData`), `poem_glyphs.png/tsv` (atlas + `CharacterInfo`), `meta.tsv`
- `ThaiMod/src/Runtime/ThaiFont.cs` (บับเบิล), `ThaiPoemFont.cs` (ฟอนต์บทกวี), `PoemLineFix.cs` (ระยะบรรทัดบทกวี)
- ข้อมูลใน `ThaiMod/data/` และไฟล์คำแปลมีข้อความในเกม จึงเก็บในเครื่องเท่านั้น (`PUBLICATION.md`)
- ตัวเลขในเกม: 67 บรรทัด, 215 กลุ่มอักษร (atlas บับเบิล 512×2048), 156 ตัวอักษรบทกวี (atlas 512×1024)

## การทดสอบ

- ทดสอบโดยผู้ใช้เล่นจริง พร้อมจับภาพหน้าต่างเกมเป็นระยะ (`import -window`, ไม่แย่ง focus) และอ่าน `NITWThai/trace.log`
- ยังไม่ได้ทดสอบ: เปิดผ่าน Steam client ด้วย launch option; ความละเอียดอื่นนอกจาก 960×540; การโหลดเซฟกลางบทกวี

## ขั้นต่อไป (เฟส 2)

- ไปป์ไลน์ดึงบททั้งเกมจาก Yarn ใน `sharedassets*.assets` เป็นไฟล์งานแปลตามบท (id, ผู้พูด, ต้นฉบับ, คำแปล, โหนด)
- รายงานบรรทัดที่แสดงแล้วแต่ยังไม่แปล (จาก trace) และตรวจเลเอาท์อัตโนมัติ (เกิน 3 บรรทัด, กลุ่มอักษรที่ฟอนต์ไม่มี)
- บรรทัดที่มีตัวแปรแทรก (~10 บรรทัด) ต้องมีทาง shape ตอนรัน
