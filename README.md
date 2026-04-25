# עיתים — ItimHebrewCalendar

לוח שנה עברי ל-Windows עם זמני היום ההלכתיים, בסגנון Fluent Design של Windows 11.

חלופה ללוח השנה המובנה של Microsoft (שלא מציג תאריך עברי), הכוללת:

- **אייקון tray דינמי** בסגנון דף לוח שנה: רצועת כותרת צבעונית עם תאריך היום העברי בגדול במרכז. כשעוברים את השקיעה (במצב המתאים) הרצועה הופכת לכתום ומופיעה שמש שוקעת.
- **חלונית מהירה מה-tray** עם לוח חודשי, פרטי היום, וזמני היום — לחיצה על יום מציגה את הפרטים בתוך אותה חלונית.
- **חלון אפליקציה ראשי** רחב יותר עם פאנל פרטי-יום קבוע מצד.
- **חלון זמני יום ייעודי** עם בחירת תאריך ומיקום.
- **ממיר תאריכים** לועזי↔עברי, עם תמיכה בקלט בגימטריה (`ט"ו`, `תשפ"ו`, `ה'תשפ"ו`).
- **ספירת העומר** מוצגת בכרטיס "היום" בתקופת הספירה (`א' בעומר`, `ל"ג בעומר`, `מ"ט בעומר`).
- **מעבר תאריך בשקיעה** (אופציונלי) — אחרי השקיעה מוצג התאריך העברי של המחר הלועזי.
- **זיהוי מיקום אוטומטי** דרך שירות המיקום של Windows.
- **שתי ספריות חישוב** — KosherJava (ברירת מחדל) או hebcal-go, לפי בחירה בהגדרות.

> ⚠️ הזמנים ההלכתיים מחושבים ממוסדות אסטרונומיים. ייתכנו סטיות של מספר דקות. אין לסמוך על הזמנים להלכה למעשה — יש לוודא מול לוח שנה מקומי.

## התקנה

1. הורד את `ItimHebrewCalendar-Setup-1.0.0.exe` מדף ה-Releases.
2. הרץ את המתקין (דורש הרשאות אדמין).
3. אם **Windows App Runtime 1.7** חסר, המתקין יציע לפתוח את דף ההורדה של Microsoft.

### דרישות מערכת

- Windows 10 build 17763 (1809) ומעלה, או Windows 11
- .NET 8 Desktop Runtime
- Windows App Runtime 1.7

## שימוש

- **לחיצה שמאלית** על אייקון ה-tray — פותחת/סוגרת את חלונית הלוח.
- **לחיצה ימנית** — תפריט עם: חלון מלא, חלונית, ממיר, זמני יום, הגדרות, אודות, יציאה.
- **לחיצה על יום בלוח** — בחלונית: מחליף את התצוגה לפרטי היום (כפתור "חזרה ללוח" למעלה). בחלון הראשי: מעדכן את הפאנל הצדדי.

## הגדרות

ב-Settings ניתן לקבוע:

- **מיקום** — 45 ערים בארץ ובחו"ל (כולל ריכוזים חרדיים: ביתר עילית, מודיעין עילית, אלעד, רכסים ועוד), או זיהוי אוטומטי דרך שירות המיקום של Windows.
- **לוח חגים** — ארץ ישראל (יום טוב אחד) או חוץ לארץ (יום טוב שני).
- **חגים מודרניים ישראליים** (יום העצמאות, יום הזיכרון, יום ירושלים, יום השואה) — מוסתרים כברירת מחדל.
- **הדלקת נרות** — דקות לפני שקיעה (ברירת מחדל 30; ירושלים/ביתר עילית/גבעת זאב = 40).
- **מעבר תאריך בשקיעה** — אם מופעל, אחרי השקיעה מוצג התאריך העברי של המחרת.
- **מנוע חישוב זמנים** — KosherJava (ברירת מחדל) או hebcal-go.
- **ערכת נושא** — בהיר, כהה, או לפי מצב המערכת.
- **תצוגת לוח** — תאריך לועזי מוקטן בכל תא, תאריך עברי באייקון ה-tray.
- **סגירה אוטומטית של חלונית tray** באיבוד פוקוס.
- **הפעלה אוטומטית** עם Windows.
- **זמני יום להצגה** — בחירה מתוך 14 זמנים (עלות, משיכיר, הנץ, סוזק"ש מג"א/גר"א וכו').

## בנייה מהמקור

### כלים נדרשים

```powershell
winget install Microsoft.DotNet.SDK.8
winget install GoLang.Go
winget install JRSoftware.InnoSetup
choco install mingw
```

### בניית ה-DLL של hebcal

```powershell
cd HebcalNative
.\build-hebcal-dll.bat
```

מייצר את `Resources\HebcalNative.dll` (binding ל-[hebcal-go](https://github.com/hebcal/hebcal-go)).

### בניית ה-EXE

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained false
```

הפלט: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\`

### יצירת ה-Setup

```powershell
cd Installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ItimHebrewCalendar.iss
```

הפלט: `Release\ItimHebrewCalendar-Setup-1.0.0.exe`

## מבנה הפרויקט

```
ItimHebrewCalendar/
├── ItimHebrewCalendar.csproj        WinUI 3 unpackaged, WinAppSDK 1.7
├── app.manifest                     DPI awareness, Windows 10/11
├── Program.cs                       Bootstrap.Initialize + single instance
├── App.xaml(.cs)                    Application + invisible anchor window
│
├── Models/
│   └── CalendarModels.cs            DTOs deserialized from HebcalNative JSON
│
├── HebcalNative/
│   ├── Services/
│   │   ├── HebcalBridge.cs                  P/Invoke bridge to HebcalNative.dll
│   │   ├── KosherJavaZmanimProvider.cs      Zmanim via KosherJava .NET port
│   │   ├── HebrewNumberFormatter.cs         int → gematria
│   │   ├── HebrewNumberParser.cs            gematria → int (also accepts decimal)
│   │   ├── OmerHelper.cs                    sefirat ha'omer day computation
│   │   ├── CitiesDatabase.cs                45 cities with coords + closest match
│   │   ├── GeoDetection.cs                  Windows.Devices.Geolocation wrapper
│   │   ├── SettingsManager.cs               AppSettings + JSON persistence
│   │   └── StartupHelper.cs                 HKCU\...\Run
│   ├── lib/
│   │   ├── hebcal_export.go                 CGO bindings to hebcal-go
│   │   └── go.mod
│   └── build-hebcal-dll.bat
│
├── Windows/
│   ├── ThemeHelper.cs               theme + RTL caption buttons
│   ├── WindowHelpers.cs             Mica, custom title bar, sizing, positioning
│   ├── CellTheme.cs                 calendar cell brushes
│   ├── TrayIconController.cs        H.NotifyIcon wiring + UI dispatcher
│   ├── TrayIconRenderer.cs          GDI+ calendar-page icon
│   ├── DayDetailsRenderer.cs        shared day-detail rendering helper
│   ├── CalendarPopup.xaml(.cs)      tray popup with inline day details
│   ├── MainWindow.xaml(.cs)         full main window with side details panel
│   ├── ZmanimWindow.xaml(.cs)       zmanim picker
│   ├── ConverterWindow.xaml(.cs)    Gregorian↔Hebrew converter
│   └── SettingsWindow.xaml(.cs)
│
├── Assets/
│   ├── AppIcon.ico                  multi-size icon
│   ├── AppIcon.png                  embedded in title bars / About
│   └── abaye.png                    author logo
│
└── Installer/
    └── ItimHebrewCalendar.iss       Inno Setup script
```

## מיקומי קבצים בזמן ריצה

- **הגדרות**: `%APPDATA%\ItimHebrewCalendar\settings.json`
- **יומן שגיאות**: `%APPDATA%\ItimHebrewCalendar\errors.log`
- **התוכנה**: `C:\Program Files\ItimHebrewCalendar\`

## ספריות חיצוניות

- **[KosherJava Zmanim](https://github.com/KosherJava/zmanim)** — חישוב זמני היום ההלכתיים, מאת Eliyahu Hershfeld. נצרך דרך ה-port ל-.NET של [Yitzchok/Zmanim](https://github.com/Yitzchok/Zmanim) (LGPL).
- **[hebcal-go](https://github.com/hebcal/hebcal-go)** — לוח שנה, חגים, פרשות וגימטריה, מאת Michael Radwin (GPL-2.0).
- **[H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)** — אייקון tray ל-WinUI 3 (MIT).
- **[Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)** — Microsoft (MIT).

## פתרון בעיות

- **"Windows App Runtime לא מותקן"** — הורד והרץ את [המתקין של Microsoft](https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe).
- **"HebcalNative.dll לא נמצא"** — הרץ את `HebcalNative\build-hebcal-dll.bat`. דורש Go + MinGW.
- **החלון נראה לבן לגמרי** — Mica לא נתמך במערכת (Windows 10 ישן). האפליקציה נופלת אוטומטית ל-Acrylic או רקע רגיל.
- **לחיצה על ה-tray לא עושה כלום** — בדוק את `%APPDATA%\ItimHebrewCalendar\errors.log` לאבחון.
- **אייקון ה-tray נעלם** — Windows מסתיר אייקונים שלא בשימוש; פתח את ה-overflow בשורת המשימות וגרור את עיתים למעלה.
- **זיהוי מיקום נכשל** — ודא ששירותי המיקום מופעלים ב-Windows ושההרשאה ניתנה לאפליקציה (Settings → Privacy → Location).

## רישוי

קוד הפרויקט: MIT.
קוד צד שלישי: KosherJava/Zmanim ב-LGPL, hebcal-go ב-GPL-2.0, H.NotifyIcon ו-Windows App SDK ב-MIT.

## פיתוח ותרומות

פותח על ידי **abaye** ([abaye.co](https://abaye.co)).
קוד פתוח — תרומות ודיווחי באגים מתקבלים בברכה ב-[GitHub](https://github.com/abaye123/ItimHebrewCalendar/issues).
