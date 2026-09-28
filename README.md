# Sky Drop (Unity)

لعبة باراشوت 3D بسيطة ومصممة عشان CrazyGames: تفتح بسرعة وتدخل في اللعب على طول، وتخلي اللاعب يقول "جولة كمان".

> **الفكرة الأساسية:** افتح الباراشوت في آخر لحظة. كل ما تفتح أوطى، المكسب يتضاعف (x1.5 → x10).
> لو اتأخرت شوية زيادة → **SPLAT**. وكمان لازم تنزل على الهدف، ولو فتحت متأخر هيبقى قدامك وقت قليل تعدّل مكانك.

---

## تشغيل المشروع

1. افتح **Unity Hub** ← **Add project from disk** واختار الفولدر ده.
   - الإصدار المقترح: **Unity 6 (6000.0 LTS)**. أي إصدار 6000.x هيشتغل.
   - لازم يكون **Built-in Render Pipeline** (المشروع مجهز كده). متنسخش الملفات جوه مشروع URP.
2. أول ما المشروع يفتح هيتعمل لوحده `Assets/Scenes/Main.unity` وهيتضبط الـ Build Settings.
   لو محصلش: القائمة **Sky Drop → Setup Project**.
3. افتح `Assets/Scenes/Main.unity` واضغط **Play**.

مفيش أي assets (موديلات، صور، أصوات): كل حاجة بتتولد بالكود. عشان كده البيلد صغير جدًا واللودينج سريع.
ده حل مباشر لمشكلة الـ **Gameplay Conversion** (33% على الموبايل في اللعبة اللي فاتت).

> لو لقيت إن الـ input مش شغال: **Project Settings → Player → Active Input Handling = Both** (أو Input Manager Old).

## التحكم

| | الكمبيوتر | الموبايل |
|---|---|---|
| التوجيه | WASD / الأسهم، أو اسحب بالماوس | اسحب في أي حتة (joystick عائم) |
| فتح الباراشوت | Space / Enter / زرار OPEN | زرار OPEN |

---

## الجيمبلاي (الجولة 20 لـ 45 ثانية)

1. **السقوط الحر:** عدّي من الحلقات (combo + سرعة + كوينز)، واجمع الكوينز، واتفادى العقبات:
   الطيور والبالونات والدرونز بيخلّوك تلف وتفقد السيطرة، والهليكوبتر والمباني والعواصف خطر.
   لو عدّيت جنب عقبة خطيرة على الحفّة بتاخد **CLOSE CALL** مع slow-mo وبونص.
2. **فتح الباراشوت:** عداد الارتفاع على اليمين بيوضح مناطق المخاطرة:
   `SAFE x1 → NICE x1.5 → BOLD x2 → RISKY x3 → CRAZY x5 → INSANE x8 → LEGENDARY x10`
   وقرب الأرض الوقت بيبطأ لوحده (bullet time) عشان الـ "آخر لحظة" تبقى مثيرة وتقدر تلحقها.
3. **الهبوط:** وجّه الباراشوت على الهدف، وعدّي حساب الهوا (الـ windsock).
   النص = PERFECT ★★★، والنص التاني = GREAT ★★، والحافة = GOOD ★، وبرا الهدف = MISSED.

## إزاي بتعالج المتريكس

| المتريكس | في اللعبة |
|---|---|
| **Gameplay conversion** | مفيش assets والبيلد صغير. أول زيارة تبدأ في اللعب على طول من غير منيو، والـ tutorial جوه أول قفزة |
| **Average playtime** | جولات قصيرة، وRetry أو Next بضغطة (أو Space). الكوينز بتفضل معاك حتى لو خسرت. فيه upgrades و50 ليفل و"So close! 1.2m" |
| **D1 retention** | Daily Gift بستريك 7 أيام، وDaily Jump (تحدي جديد كل يوم x2 كوينز +150)، وskins، وعوالم بتتفتح، ونجوم |

### المحتوى
- **5 عوالم × 10 ليفلات:** Green Valley، وPyramid Desert (فيها أهرامات وهدف على تريلا ماشية)، وTropical Islands (الهدف على مركب)، وSnow Peaks (عواصف ورياح قوية)، وNeon City (بالليل، ناطحات سحاب، والهدف على سطح).
- **5 Upgrades:** Quick Chute (بيوسّع مناطق المخاطرة)، وWingsuit، وCoin Magnet، وShield، وCanopy Control.
- **8 Skins** و**Daily Gift** و**Daily Jump**.

---

## CrazyGames SDK

الكود شغال من غير الـ SDK (الحفظ في PlayerPrefs، والإعلانات بتعدّي على طول).
عشان تفعّله:
1. نزّل **CrazyGames Unity SDK (v3)** من docs.crazygames.com واعمله import.
2. **Project Settings → Player → Scripting Define Symbols** وضيف `CRAZY_SDK`.
3. كل الاستدعاءات في `Assets/Scripts/Core/PlatformSDK.cs`: `GameplayStart/Stop` و`HappyTime` و`Midgame` و`Rewarded` و`Data` (الحفظ على حساب اللاعب).
   لو فيه أي اسم في الـ SDK مختلف في الإصدار اللي عندك، الـ compiler هيوريك السطر بالظبط.

الإعلانات:
- **Midgame:** بين الجولات بس (Next/Retry)، كل 3 جولات، وبين كل إعلان والتاني دقيقتين ونص على الأقل.
- **Rewarded:** زرار "x2 COINS" في شاشة النتيجة، واختياري.

## Build لـ WebGL

**File → Build Profiles → Web → Build.** سكريبت الـ Setup بيضبط Brotli وData caching وstripping.
لو السيرفر اللي بتجرب عليه محليًا مش بيدعم Brotli headers فعّل **Decompression Fallback** وانت بتجرب.

---

## فين أظبط الصعوبة؟

| عايز تغير | الملف |
|---|---|
| ارتفاع الليفل، حجم الهدف، عدد الحلقات والعقبات، الرياح | `Assets/Scripts/Gameplay/Levels.cs` → `Levels.Make` |
| مناطق المخاطرة والـ multipliers | `Assets/Scripts/Gameplay/Zones.cs` |
| سرعة السقوط، سرعة الباراشوت، سرعة الهبوط الآمنة | `Assets/Scripts/Gameplay/Jumper.cs` |
| أسعار الـ upgrades والـ skins والمكافآت اليومية | `Assets/Scripts/Core/Economy.cs` |
| المكافآت والنجوم وفتح الليفلات | `GameManager.BuildResult` |

## هيكل الكود

```
Assets/
  Resources/Shaders/     شيدرز low-poly (إضاءة + fog بدون keywords عشان WebGL)
  Scripts/Core/          GameManager (القواعد والفلو)، الحفظ، الاقتصاد، الصوت المتولد، الـ input، الـ SDK
  Scripts/Gameplay/      Jumper (الفيزيا + الجسم + الباراشوت)، LevelBuilder، Level، CameraRig، Fx، Zones
  Scripts/UI/            GameUI (كل الشاشات بالكود)، UIKit
  Editor/                SkyDropSetup (سين + إعدادات WebGL)
```
