# Adventure3D — النواة التقنية

لعبة قتالية عربية ثلاثية الأبعاد. هذه المرحلة برمجية فقط: حركة لاعب بإحساس وزن قابل للقياس، وثلاثة أنماط لعب (أوفلاين، شبكة محلية، خادم مستقل) من نفس المعمارية، وواجهة عربية وظيفية، ومستوى فحص هندسي بأشكال Unity الافتراضية.

- المحرر: **Unity 6000.0.85f2** (الإصدار المعتمد والمثبت فعلياً على هذا الجهاز).
- كل الأوامر أدناه تُنفذ في PowerShell من مجلد المشروع `C:\Games\wesam games`.

---

## ١. أقصر طريق: جرّب الأوفلاين الآن

النسخة المبنية جاهزة:

```powershell
& ".\Builds\Windows\Adventure3D.exe"
```

اختر «بدء المهمة». تبدأ جولة قتال أوفلاين مع 5 بوتات (حليفان و3 خصوم). لا يُفتح أي اتصال شبكي، ويعمل حتى لو كان الإنترنت مفصولاً من أول تشغيل.

| التحكم | لوحة المفاتيح والفأرة | يد التحكم | اللمس |
|---|---|---|---|
| حركة | WASD أو الأسهم | العصا اليسرى | العصا العائمة يسار الشاشة |
| نظر | الفأرة (انقر داخل النافذة لقفل المؤشر) | العصا اليمنى | السحب في النصف الأيمن |
| قفز | Space | A / Cross | زر «قفز» |
| ركض | Shift | L1 أو ضغطة العصا اليسرى | زر «ركض» |
| إطلاق | زر الفأرة الأيسر | RT / R2 | زر «إطلاق» (السحب عليه يصوّب أيضاً) |
| تصويب (منظار) | زر الفأرة الأيمن | LT / L2 | — |
| تعبئة | R | X / مربع | زر «تعبئة» |
| تبديل السلاح | Q أو عجلة الفأرة أو 1/2/3 | Y / مثلث | زر «تبديل» |
| شراء / نتائج | B / Tab | — | — |
| القائمة / الإيقاف | Esc | Start / Options | زر «قائمة» |
| رجوع في القوائم | Esc | B / Circle | — |

ساحة الفحص فيها: ممر 20 م بعلامات صفراء كل 5 م، درج 8 درجات (25 سم)، منحدر أخضر 40° (يُصعد)، منحدر أحمر 50° (لا يُصعد)، حافة بنفسجية بارتفاع 3 م، سقف منخفض (2.3 م)، ونفق أقل من طول اللاعب.

### من داخل Unity

1. Unity Hub ← Add ← اختر المجلد `C:\Games\wesam games` ← افتحه بالإصدار 6000.0.85f2.
2. اضغط Play. زر Play يبدأ دائماً من `Assets/Game/Scenes/Bootstrap.unity` ثم القائمة العربية، أياً كان المشهد المفتوح. لإيقاف ذلك (مثلاً لتشغيل `TestArena.unity` مباشرة أوفلاين) ألغِ تفعيل قائمة **Adventure3D ← Play From Bootstrap**.

---

## ٢. شبكة محلية: مضيف وعميل

على الجهاز الأول (المضيف):

```powershell
& ".\Builds\Windows\Adventure3D.exe" -mode host -port 7777
```

أو من القائمة: «إنشاء جلسة محلية» ← «ابدأ الجلسة». عنوان الاستماع والمنفذ يظهران أعلى يمين الشاشة باتجاه يسار إلى يمين، مثل `192.168.1.20:7777`.

على الجهاز الثاني (أو نسخة ثانية على نفس الجهاز):

```powershell
& ".\Builds\Windows\Adventure3D.exe" -mode client -address 192.168.1.20 -port 7777
```

أو من القائمة: «الانضمام إلى جلسة» ثم اكتب العنوان والمنفذ ثم «اتصال». على نفس الجهاز استخدم `127.0.0.1`.

> أول مرة يستمع فيها المضيف قد يسأل جدار حماية Windows عن السماح. اسمح بالشبكة الخاصة حتى يصل إليه جهاز آخر في الشبكة المحلية.

---

## ٣. الخادم المستقل والعملاء

خادم بلا واجهة رسومية ولا كاميرا ولا إدخال:

```powershell
& ".\Builds\Windows\Adventure3D.exe" -batchmode -nographics -mode server -port 7777 -logFile server.log
```

عميلان (كل واحد في نافذة):

```powershell
& ".\Builds\Windows\Adventure3D.exe" -mode client -address 127.0.0.1 -port 7777
```

```powershell
& ".\Builds\Windows\Adventure3D.exe" -mode client -address 127.0.0.1 -port 7777
```

العنوان يُضبط من القائمة («الاتصال بخادم»)، أو من سطر الأوامر، أو من ملف `adventure3d-settings.json` بجانب الملف التنفيذي:

```json
{ "onlineServerAddress": "play.example.com", "port": 7777 }
```

إيقاف الخادم: أغلق نافذة PowerShell أو `Stop-Process -Name Adventure3D`. العملاء يعرضون «أغلق المضيف الجلسة» أو «انقطع الاتصال بالخادم» ثم يرجعون للقائمة.

خيارات اختبار إضافية: `-simLatency 100 -simJitter 10` (تأخير مصطنع بالمللي ثانية)، `-simLoss 5` (فقد حزم %)، `-protocol 999` (محاكاة إصدار غير متوافق)، `-touchui` (إظهار أزرار اللمس على الكمبيوتر)، `-fps 30` (تحديد معدل الإطارات)، `-profile اسم` (هوية ضيف منفصلة).

---

## ٤. البناء والاختبارات

```powershell
powershell -ExecutionPolicy Bypass -File .\Assets\Game\Tests\MultiProcess\Run-AllChecks.ps1
```

يشغّل بالترتيب: توليد المشاهد والإعدادات، اختبارات Edit Mode وPlay Mode، بناء Windows (وتسجيل حالة Android وiOS وPlayStation)، ثم سيناريوهات عدة عمليات: أوفلاين، LAN، خادم مستقل وعميلان، حالات الفشل والتأخير، ولقطات الواجهة. النتائج في `TestResults\` و`Reports\`.

كل خطوة لها سكربت مستقل في `Assets\Game\Tests\MultiProcess\`:

| السكربت | ماذا يفعل |
|---|---|
| `Run-UnityTests.ps1 [-Setup]` | Edit Mode + Play Mode في batchmode |
| `Build-Players.ps1 [-All]` | بناء `Builds\Windows\Adventure3D.exe` |
| `Test-Offline.ps1` | هوية الضيف بعد إعادة التشغيل، لا اتصالات شبكية، جلستان متتاليتان، 30/60/120 إطاراً |
| `Test-Lan.ps1` | مضيف + عميل، رؤية متبادلة، خروج وإعادة انضمام دون نسخة قديمة |
| `Test-DedicatedServer.ps1` | خادم مستقل + عميلان، ثم إغلاق نظامي وإغلاق قسري |
| `Test-NetworkEdgeCases.ps1` | إصدار غير متوافق، مهلة، عنوان غير صالح، تأخير 100 مللي ثانية |
| `Capture-UiScreenshots.ps1 [-Touch]` | لقطات للقائمة والخطأ والـHUD والإيقاف |

---

## ٥. أين أعدّل؟

باختصار (التفاصيل في [Docs/Architecture.md](Docs/Architecture.md)):

- **سرعة الركض:** `Assets/Game/Configuration/Resources/Adventure3D/MovementProfile.asset` ← `SprintSpeed`.
- **ارتفاع القفزة والجاذبية:** `AirMovementProfile.asset` ← `JumpHeight` و`TimeToApex` (الجاذبية تُحسب منهما)، و`FallGravityMultiplier` و`MaxFallSpeed`.
- **إعدادات الشبكة:** `NetworkProfile.asset`، أو `adventure3d-settings.json`، أو سطر الأوامر.
- **أول شخصية:** `Assets/Game/Gameplay/Resources/Adventure3D/Player.prefab`، تحت `View` (بدل الكبسولة `Body`).
- **Animator:** على النموذج تحت `View`، ويقرأ `PlayerAvatar.State` و`MotorEventsRaised`.
- **كاميرا منظور ثالث:** نفّذ `ICameraRig` واستبدل `FirstPersonCameraRig` في `ArenaEntryPoint`.
- **مزود الحسابات:** نفّذ `IAccountProvider` وسلّمه في `AppRoot.Initialize`. التفاصيل في [Docs/Identity.md](Docs/Identity.md).

## المستندات

- [Docs/Architecture.md](Docs/Architecture.md): الهيكل ومسار التشغيل وأماكن التعديل.
- [Docs/Networking.md](Docs/Networking.md): البروتوكول وسبب اختيار طبقة الشبكة والأخطاء المعالجة.
- [Docs/Identity.md](Docs/Identity.md): هوية الضيف وعقد الحسابات المستقبلية.
- [Docs/Platforms.md](Docs/Platforms.md): المنصات وما يحتاج ترخيصاً أو جهازاً.
- [Docs/Combat.md](Docs/Combat.md): القتال والأسلحة والبوتات، واليدان والسلاح، وكيف يُستبدلان بنموذج حقيقي.
- [Reports/VerificationReport.md](Reports/VerificationReport.md): تقرير التحقق والنتائج الفعلية.
