# المنصات

## الحالة في هذه البيئة

المحرر المثبت `Unity 6000.0.85f2` فيه وحدة **Windows Build Support (Mono)** فقط. لا توجد وحدات Android ولا iOS ولا Windows Dedicated Server ولا PlayStation.

| المنصة | تجهيز المشروع | تجميع السكربتات | البناء | التشغيل على جهاز |
|---|---|---|---|---|
| Windows PC (x64) | نجح | نجح | نجح (`Builds/Windows/Adventure3D.exe`) | نجح على هذا الجهاز |
| خادم Windows بلا رسوم | نجح (نفس الملف التنفيذي مع `-batchmode -nographics`) | نجح | نجح (نفس البناء) | نجح (localhost) |
| Android | نجح: أفقي فقط، IL2CPP، ARM64، API 24+، إذن الإنترنت، `com.wesamgames.adventure3d` | متعذر في البيئة الحالية | متعذر في البيئة الحالية | لم يُختبر |
| iPhone / iPad | نجح: أفقي فقط، IL2CPP، iOS 15+ | متعذر في البيئة الحالية | تصدير مشروع Xcode متعذر في البيئة الحالية | لم يُختبر |
| PlayStation | المسار المشترك جاهز (يد التحكم، القائمة، الواجهة) | متعذر في البيئة الحالية | متعذر في البيئة الحالية | لم يُختبر |

نجاح يد التحكم على الكمبيوتر ليس دليلاً على نجاح بناء PlayStation.

## ما يلزم لكل منصة

### Android
1. Unity Hub ← Installs ← 6000.0.85f2 ← Add modules ← **Android Build Support** مع OpenJDK وAndroid SDK & NDK.
2. `Build-Players.ps1 -All` سيبني `Builds/Android/Adventure3D.apk` تلقائياً.
3. للتشغيل: فعّل وضع المطور وUSB debugging على الهاتف، ثم `adb install Builds\Android\Adventure3D.apk`.
4. الخط: الواجهة تستخدم خطوط النظام (`Noto Naskh Arabic` في Android). إذا لم تظهر الحروف موصولة على جهاز ما، أضيفوا خطاً عربياً مرخصاً كأصل في المشروع (بند فني لاحق).

### iPhone / iPad
1. أضف وحدة **iOS Build Support**. تصدير مشروع Xcode يعمل من Windows.
2. بناء التطبيق والتشغيل على جهاز يحتاجان: جهاز Mac عليه Xcode، وحساب Apple Developer، وتوقيع.
3. لعب LAN على iOS 14+ يحتاج إضافة `NSLocalNetworkUsageDescription` إلى Info.plist (سكربت post-process لاحق).

### PlayStation
- **الجيل المستهدف قرار مفتوح.** افترضت PS5 في الإعدادات والتوثيق لأنه الجيل الحالي، ويمكن تغييره.
- يلزم: تسجيل شريك لدى Sony، ووحدة PlayStation المرخصة لـUnity، وSDK، وجهاز تطوير (DevKit).
- كل تكامل خاص بالجهاز يوضع في `Assets/Game/Platform` خلف `#if UNITY_PS5` حتى تستمر بقية المنصات في التجميع. يد التحكم تُقرأ الآن عبر `Gamepad` العام في Input System، وهو نفس المسار على DualSense.

## ملاحظات أداء

لم أقِس أداء الهاتف ولا الجهاز المنزلي. قياسات الحركة في التقرير محاكاة ثابتة 60 هرتز على Windows، ولا يُستنتج منها أداء الأجهزة الأخرى.
