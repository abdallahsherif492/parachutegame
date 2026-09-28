# Sumo Noodles — الرفع على fly.io و CrazyGames

## 1) سيرفر الأونلاين على fly.io (مرة واحدة)

محتاج حساب على fly.io، و`flyctl` متسطب على جهازك.

```bash
# تسطيب flyctl
# Windows (PowerShell):  iwr https://fly.io/install.ps1 -useb | iex
# Mac / Linux:           curl -L https://fly.io/install.sh | sh

fly auth login

cd sumo-noodles/server
fly launch --no-deploy --copy-config --name sumo-noodles-XXXX   # اختار اسم مش مستخدم
fly deploy
```

- **الاسم:** بعد `fly launch` افتح `fly.toml` واتأكد إن `app = "..."` هو نفس الاسم اللي اخترته.
- **رابط السيرفر هيبقى:** `wss://sumo-noodles-XXXX.fly.dev`
- **اختبار:** افتح `https://sumo-noodles-XXXX.fly.dev/stats` في المتصفح. المفروض يظهر `{"online":0,...}`.
- **التكلفة:** السيرفر شغال على جهاز صغير واحد (shared-cpu-1x، 256MB) وشغال على طول. ده أرخص مقاس على fly.io.
- **المنطقة:** `fra` (فرانكفورت). ليه؟ عشان الماتش ميكونش بطيء لا على أوروبا ولا على مصر.

## 2) بناء نسخة CrazyGames

```bash
cd sumo-noodles
python3 build/build_crazygames.py --server wss://sumo-noodles-XXXX.fly.dev
```

الأمر ده بيطلع `dist/sumo-noodles-crazygames.zip`، وجواه ملف واحد `index.html`:
- CrazyGames SDK v3 شغال.
- الخطوط متضمنة جوه الملف، فمفيش أي طلبات لمواقع برة.
- رابط السيرفر مكتوب جوه النسخة.

## 3) الرفع على CrazyGames Developer Portal

- **Game files:** ارفع `dist/sumo-noodles-crazygames.zip` (نوع HTML5).
- **Cover images:** من `dist/store/`:
  - `cover-1920x1080.png`
  - `cover-800x1200.png`
  - `cover-800x800.png`
- **Video:**
  - `dist/store/sumo-noodles-trailer-land.mp4` (1920×1080، 26 ثانية، من غير صوت)
  - نسخة طولية للموبايل: `sumo-noodles-trailer-port.mp4`
- **Multiplayer:**
  - اختار إن اللعبة فيها multiplayer.
  - زرار الدعوة بتاع CrazyGames شغال: اللعبة بتستخدم `inviteLink` و`showInviteButton` و`getInviteParam("roomId")`.
  - لو فعّلت Instant Multiplayer، اللعبة هتفتح أوضة خاصة لوحدها أول ما تفتح.
- **Controls:**
  - A / D أو الأسهم: تشحن وتندفع يمين أو شمال.
  - W أو Space: تندفع ناحية الخصم.
  - موبايل: الضغط على نص الشاشة الشمال أو اليمين.

> راجع المقاسات والطول المطلوب للفيديو والصور في البورتال نفسه وقت الرفع، لأن CrazyGames ممكن تغيرها.

## إزاي الأونلاين شغال (للمطور)

- **السيرفر** (`server/server.js`) بيعمل حاجتين بس:
  - يطابق لاعبين، إما Quick Match أو كود أوضة من 5 حروف.
  - يوصّل الرسايل بينهم.
- **أول لاعب في الماتش هو الـ host:**
  - المتصفح بتاعه بيشغّل الفيزيكس.
  - بيبعت حوالي 30 لقطة في الثانية، وفيها أماكن الأجسام والأصوات والتأثيرات.
- **اللاعب التاني:**
  - بيبعت ضغطات الزراير بس.
  - بيرسم اللقطات متأخر 100ms عشان تبان ناعمة.
- **الماتش:** أول واحد يوصل 3 نقط يكسب، وبعدها ينفع تعمل Rematch أو تدور على خصم جديد.
- **لو محدش أونلاين:** بعد 12 ثانية بيظهر زرار PLAY VS CPU.
