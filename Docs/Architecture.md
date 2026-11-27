# هيكل المشروع ومكان كل تعديل

كل كود اللعبة تحت `Assets/Game`. كل مجلد له Assembly Definition خاصة به، والمراجع بينها تمنع الاعتماد الخاطئ: طبقات الخادم (`Core`, `Configuration`, `Gameplay`, `Identity`, `Networking`) لا ترى `UnityEngine.UI` ولا `Unity.InputSystem` ولا الواجهة، وهذا مفحوص آلياً في `ProjectIntegrityTests`.

```
Assets/Game
├── Bootstrap      نقطة الدخول: AppRoot (نسخة واحدة)، AppBootstrap، ArenaEntryPoint، LaunchOptions، DiagnosticsAgent
├── Core           العقود المشتركة: PlayerCommand، PlayerSimState، SessionMode/Status/EndReason، ProtocolInfo، GameLog
├── Configuration  ملفات الضبط ScriptableObject: Movement / AirMovement / Physics / Camera / Input / Network + GameConfig
├── Gameplay       PlayerMotor (خطوة المحاكاة الوحيدة)، PlayerAvatar، WorldSimulation، FirstPersonCameraRig، TestArenaLayout
├── Input          LocalPlayerInput (لوحة مفاتيح وفأرة، يد تحكم، لمس) + TouchControlsModel
├── Networking     GameSession (الأنماط الأربعة)، ServerRole، ClientRole، NetMessages، RemoteInterpolation، SessionRunner
├── Identity       PlayerIdentity، GuestIdentityProvider، IAccountProvider (عقد الحسابات المستقبلية)
├── Presentation   ArabicShaper، Strings (ar.txt)، القائمة، HUD، الإيقاف، رسائل الأخطاء، أزرار اللمس، SafeArea
├── Platform       PlatformInfo، AppFocus، SafeArea (كل فرق خاص بمنصة يوضع هنا خلف #if)
├── Editor         ProjectSetup (يولّد المشاهد والأصول والإعدادات)، BuildScripts، UnitDrawer
├── Scenes         Bootstrap.unity → MainMenu.unity → TestArena.unity
└── Tests          EditMode، PlayMode، MultiProcess (سكربتات PowerShell لعدة عمليات)
```

## مسار التشغيل

1. `Bootstrap.unity` (رقم 0 في Build Settings) فيه `AppBootstrap`، ينشئ `AppRoot` واحداً فقط (`DontDestroyOnLoad`) يقرأ الإعدادات ووسائط سطر الأوامر ويسجّل دخول الضيف.
2. بلا `-mode` يفتح `MainMenu.unity` (القائمة العربية). مع `-mode` يبدأ الجلسة مباشرة (الخادم المستقل والاختبارات الآلية).
3. `TestArena.unity` فيه `ArenaEntryPoint` الذي ينشئ `GameSession` و`SessionRunner`، ثم الإدخال والكاميرا والـHUD **فقط** إذا كان للعملية لاعب محلي.
4. الخروج إلى القائمة يحذف مشهد الساحة، فيُتلف `SessionRunner` الجلسة ويغلق الاتصال ويحذف اللاعبين. لهذا لا يتكرر لاعب ولا مستمع ولا مدير شبكة عند بدء جلسة ثانية (مفحوص في الاختبارات وفي البناء).

## نظام حركة واحد لكل الأنماط

`PlayerMotor.Step` هي الدالة الوحيدة التي تحرك اللاعب. يستدعيها:
- الأوفلاين (أوامر الجهاز مباشرة)،
- المضيف والخادم المستقل (أوامر كل اتصال)،
- العميل للتنبؤ بلاعبه ثم لإعادة تشغيل الأوامر بعد تصحيح الخادم.

المحاكاة بمعدل ثابت 60 تكة في الثانية (`PhysicsProfile.TickRate`). الإدخال والكاميرا يُقرآن كل إطار، والعرض يُستوفى بين تكتين، لذلك لا تتغير الحركة مع معدل الإطارات (القياس: انحراف 0 م بين 30 و60 و120 إطاراً).

## أين أعدّل؟

| ماذا | أين | الحقل |
|---|---|---|
| سرعة المشي والركض | `Assets/Game/Configuration/Resources/Adventure3D/MovementProfile.asset` | `WalkSpeed`، `SprintSpeed` (م/ث) |
| التسارع والتباطؤ (إحساس الوزن) | نفس الملف | `GroundAcceleration`، `GroundDeceleration` (م/ث²) |
| حد المنحدر وارتفاع الدرجة | نفس الملف | `MaxWalkableSlope` (درجة)، `StepHeight` (م) |
| ارتفاع القفزة | `AirMovementProfile.asset` | `JumpHeight` (م)، `TimeToApex` (ث) |
| الجاذبية | `AirMovementProfile.asset` | تُشتق تلقائياً: `g = 2h/t²`. ثقل السقوط: `FallGravityMultiplier`، الحد: `MaxFallSpeed` |
| مهلة القفز بعد الحافة وتخزين الضغطة | `AirMovementProfile.asset` | `CoyoteTimeEnabled/CoyoteTime`، `JumpBufferEnabled/JumpBufferTime` |
| حجم الكبسولة وارتفاع العين ومعدل المحاكاة | `PhysicsProfile.asset` | `CapsuleHeight`، `CapsuleRadius`، `EyeHeight`، `TickRate` |
| الكاميرا | `CameraProfile.asset` | `FieldOfView`، حدود النظر |
| حساسية الأجهزة والمنطقة الميتة | `InputProfile.asset` | الفأرة، العصا، اللمس، كل واحدة مستقلة |
| الشبكة | `NetworkProfile.asset` + ملف `adventure3d-settings.json` + وسائط سطر الأوامر | العنوان، المنفذ، المهلات، التنعيم، التأخير المصطنع |

كل حقل يظهر في Inspector مع وحدته، مثل «Walk Speed (m/s)». قيم كل لاعب وقت التشغيل موجودة في `PlayerSimState` وليست في هذه الملفات.

## ربط الشخصية والـAnimator والكاميرا والحسابات لاحقاً

- **الشخصية:** افتح `Assets/Game/Gameplay/Resources/Adventure3D/Player.prefab`. الجذر `Player` هو جذر المحاكاة (فيه `CharacterController` و`PlayerAvatar`) ولا يُلمس. ضع نموذج الشخصية تحت `View` واحذف الكبسولة المؤقتة `Body` و`FacingMarker`، ثم أضف رندررات النموذج إلى `bodyRenderers` حتى تُخفى في المنظور الأول.
- **Animator:** على النموذج تحت `View`. اقرأ `PlayerAvatar.State` (`Grounded`، `Sprinting`، `HorizontalSpeed`، `VerticalSpeed`) واشترك في `PlayerAvatar.MotorEventsRaised` (`Jumped`، `Landed`، `LeftGround`، `HitCeiling`). هذه القيم تصل أيضاً للاعبين البعيدين عبر اللقطات.
- **كاميرا منظور ثالث:** نفّذ `ICameraRig` (الموجود في `Gameplay/Camera`) واتبع `ViewRoot`، ثم استبدل `FirstPersonCameraRig.Create()` في `ArenaEntryPoint`. لا يتغير شيء في الحركة أو البروتوكول.
- **مزود الحسابات:** نفّذ `IAccountProvider` وسلّمه لـ `IdentityService` في `AppRoot.Initialize`. نتيجة المصادقة (`SessionToken`) تُرسل تلقائياً في رسالة `Hello` إلى الخادم، والتحقق منها مكانه `ServerRole.HandleHello`. التفاصيل في `Docs/Identity.md`.
