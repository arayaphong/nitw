# NITW reconstruction tooling

เก็บเฉพาะเครื่องมือและโค้ดสนับสนุนที่เขียนขึ้นระหว่างศึกษาเกม Night in the Woods และสร้างต้นแบบ Unity บน Linux

**นี่เป็น source archive บางส่วน ไม่ใช่เกมหรือ Unity project ที่ clone แล้ว build ได้ทันที** ตัวรันที่ใช้งานในเครื่องยังต้องใช้ข้อมูลเกมและโค้ดส่วนที่ไม่ได้เผยแพร่ใน repository นี้

สิ่งที่มี:

- Python readers/exporters สำหรับข้อมูล Unity และ metadata/CIL จากไฟล์ที่ผู้ใช้มีอยู่ในเครื่อง
- Unity editor importers, build adapters และตัวตรวจสอบที่เราเขียน
- Data schemas, ตัววาดข้อความไทยจาก glyph ที่จัดรูปไว้ และ native API bindings ที่เราเขียน
- เครื่องมือตรวจรายการไฟล์ก่อนเผยแพร่ พร้อม manifest ของ source ทุกไฟล์

สิ่งที่ไม่อัปโหลด: ภาพ/สไปรต์ โลโก้ เสียง เพลง ฟอนต์ DLL/native libraries/banks ตัวเกมหรือ build ฉาก/animation curves/materials ที่สกัดออกมา บทสนทนา บทกวีและคำแปล รายงาน CIL/decompile รวมถึงโค้ด runtime/shader ที่แปลตาม implementation เดิมอย่างใกล้ชิด ดู [PUBLICATION.md](PUBLICATION.md) สำหรับขอบเขตและการแก้ข้อมูลเฉพาะเครื่องออกจาก source

## ใช้เครื่องมือในเครื่อง

```bash
python3 -m venv .tools/unity-inspect
.tools/unity-inspect/bin/pip install -r requirements-tools.txt
export NITW_WORKSPACE="$PWD"
export NITW_GAME_DATA="/path/to/your/installed/Night in the Woods_Data"
```

ก่อนใช้ exporter แต่ละตัว ให้ดู input/output ใน source และเตรียมโฟลเดอร์ output ภายในเครื่อง เครื่องมือบางตัวต้องใช้ผลลัพธ์จากขั้นก่อนหน้าซึ่งไม่ได้แจกมาด้วย และ `prepare_intro_poem_th.py` ต้องตั้ง `NITW_THAI_FONT` เป็นไฟล์ฟอนต์ที่ตนมีสิทธิ์ใช้งาน พร้อมเตรียม translation input ในเครื่องเอง

เครื่องมือเหล่านี้อาจสร้างไฟล์ภาพ ฟอนต์ เสียง ข้อมูลฉาก หรือ CIL ในเครื่อง ผลลัพธ์เหล่านั้น **ไม่ใช่ส่วนหนึ่งของชุด source ที่อนุมัติให้เผยแพร่** และต้องไม่เพิ่มเข้า Git โดยอัตโนมัติ ไม่มีตัวติดตั้งหรือดาวน์โหลดเกมรวมอยู่ด้วย

C# importers/diagnostics เป็นส่วนที่ใช้ร่วมกับต้นแบบ Unity 6000.3.28f1 ในเครื่องต้นทาง มี references ถึง runtime และ resources ที่จงใจไม่เผยแพร่ จึงไม่ได้อ้างว่า C# snapshot นี้ compile ได้ลำพัง ส่วน manifest/audit และ Python syntax ตรวจได้แยกจาก Unity

## ตรวจชุดเผยแพร่

```bash
python3 scripts/check_publication.py
python3 -m unittest discover -s tests -v
# หลังสร้าง commit: ตรวจ bytes ใน commit จริง
python3 scripts/check_publication.py --revision HEAD
# เปิดใช้ hook สำหรับการ push จากเครื่องนี้
 git config core.hooksPath .githooks
```

`.gitignore` ใช้ allowlist รายไฟล์ และ audit ตรวจชื่อไฟล์ ชนิดไฟล์ UTF-8 และ SHA-256 ตรงกับ manifest การเพิ่ม source ใหม่ต้องตรวจที่มาและปรับ manifest อย่างตั้งใจ การตรวจอัตโนมัติเหล่านี้ช่วยกันไฟล์หลุด แต่ไม่ใช่การยืนยันสิทธิ์ทางกฎหมายของ source ใหม่ทุกชนิด

LICENSE ที่เจ้าของ repository เลือกไว้เดิมยังคงอยู่ ไม่ครอบคลุมหรือให้สิทธิ์ในเนื้อหาของเกม ซอฟต์แวร์ Unity หรือไลบรารี/ฟอนต์ของบุคคลอื่น Repository นี้ไม่ได้เป็นโครงการอย่างเป็นทางการของผู้สร้างเกม
