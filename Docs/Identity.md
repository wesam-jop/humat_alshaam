# الهوية والحسابات

## ثلاثة معرفات منفصلة

| المعرف | من يصدره | أين يعيش | يبقى بعد إعادة التشغيل؟ |
|---|---|---|---|
| `PlayerIdentity.LocalProfileId` | الجهاز (GUID عند أول تشغيل) | `persistentDataPath/identity/guest-slotN.json` | نعم |
| معرف اللاعب في الجلسة (`ushort`) | الخادم عند قبول الاتصال | `GameSession.LocalPlayerId` فقط | لا، جديد في كل جلسة |
| `PlayerIdentity.AccountId` | مزود حسابات مستقبلي | فارغ حالياً | (لاحقاً) |

على Windows مسار الملف: `%USERPROFILE%\AppData\LocalLow\WesamGames\Adventure3D\identity\`.

إذا شُغّلت نسختان على نفس الجهاز تأخذ كل واحدة «خانة» مقفلة مختلفة (`guest-slot0`، `guest-slot1`)، فلكل نسخة معرف ثابت مختلف. `-profile <name>` يفرض ملفاً محدداً.

## الدخول كضيف

`GuestIdentityProvider` لا يحتاج شبكة، ويعمل قبل أي اتصال. اللعب الأوفلاين لا يعتمد على أي مزود حسابات.

## نقطة الربط المستقبلية

`Identity/IdentityContracts.cs`:
```csharp
public interface IAccountProvider
{
    bool IsAvailable { get; }
    Task<AuthResult> CreateAccountAsync(AccountCredentials credentials, string displayName);
    Task<AuthResult> SignInAsync(AccountCredentials credentials);
    Task SignOutAsync();
    Task<AuthResult> TryRestoreSessionAsync();
}
```
المطبّق الوحيد الآن `UnavailableAccountProvider`، ويرجع `NotConfigured` دائماً. القائمة تكتب «الحسابات غير متاحة بعد، اللعب كضيف فقط» ولا توجد شاشة تسجيل توحي بنجاح مصادقة غير موجودة.

### كيف يُربط مزود حقيقي

1. نفّذ `IAccountProvider` (مثلاً `MyBackendAccountProvider`) في مجلد `Identity` أو في Assembly جديدة.
2. في `AppRoot.Initialize` بدّل `new UnavailableAccountProvider()` بمزودك.
3. بعد نجاح `SignInAsync` اضبط `PlayerIdentity.Kind = Account` و`AccountId` و`SessionToken`.
4. `GameSession` يرسل `SessionToken` في `Hello.AuthToken`. على الخادم، تحقّق منه في `ServerRole.HandleHello` قبل `Spawn`، وارفض بسبب جديد في `RejectReason` إذا فشل.
5. واجهة الحساب (إنشاء، دخول، خروج) تضاف كشاشة جديدة في `Presentation/Menu` تستدعي المزود. إذا فشل المزود يبقى زر «لعب فردي» يعمل.
