package main

// CGO bridge to hebcal-go. Built into HebcalNative.dll and consumed via P/Invoke
// from the C# side. Every export returns a malloc'd C string that the caller
// must free via FreeString to avoid leaking Go heap memory.

/*
#include <stdlib.h>
*/
import "C"

import (
	"encoding/json"
	"fmt"
	"strings"
	"time"
	"unsafe"

	_ "time/tzdata" // embed IANA tzdata so the DLL is self-contained

	"github.com/hebcal/hdate"
	"github.com/hebcal/hebcal-go/event"
	"github.com/hebcal/hebcal-go/hebcal"
	"github.com/hebcal/hebcal-go/zmanim"
)

func main() {}

func toCString(v interface{}) *C.char {
	b, err := json.Marshal(v)
	if err != nil {
		return C.CString(fmt.Sprintf(`{"error":"%s"}`, err.Error()))
	}
	return C.CString(string(b))
}

//export FreeString
func FreeString(s *C.char) {
	C.free(unsafe.Pointer(s))
}

var hebMonthNamesHe = map[hdate.HMonth]string{
	hdate.Nisan:    "ניסן",
	hdate.Iyyar:    "אייר",
	hdate.Sivan:    "סיון",
	hdate.Tamuz:    "תמוז",
	hdate.Av:       "אב",
	hdate.Elul:     "אלול",
	hdate.Tishrei:  "תשרי",
	hdate.Cheshvan: "חשוון",
	hdate.Kislev:   "כסלו",
	hdate.Tevet:    "טבת",
	hdate.Shvat:    "שבט",
	hdate.Adar1:    "אדר",
	hdate.Adar2:    "אדר ב'",
}

func hebMonthNameHebrew(m hdate.HMonth, hy int) string {
	if m == hdate.Adar1 && hdate.IsLeapYear(hy) {
		return "אדר א'"
	}
	if n, ok := hebMonthNamesHe[m]; ok {
		return n
	}
	return ""
}

func intToHebrew(n int) string {
	if n <= 0 {
		return ""
	}
	ones := []string{"", "א", "ב", "ג", "ד", "ה", "ו", "ז", "ח", "ט"}
	tens := []string{"", "י", "כ", "ל", "מ", "נ", "ס", "ע", "פ", "צ"}
	hundredChars := []string{"", "ק", "ר", "ש", "ת"}

	var sb strings.Builder
	hundreds := n / 100
	n = n % 100

	if hundreds > 0 && hundreds <= 4 {
		sb.WriteString(hundredChars[hundreds])
	} else if hundreds >= 5 && hundreds <= 8 {
		sb.WriteString("ת")
		sb.WriteString(hundredChars[hundreds-4])
	}

	if n == 15 {
		sb.WriteString("טו")
		n = 0
	} else if n == 16 {
		sb.WriteString("טז")
		n = 0
	} else {
		t := n / 10
		sb.WriteString(tens[t])
		n = n - t*10
		sb.WriteString(ones[n])
	}

	res := sb.String()
	r := []rune(res)
	if len(r) == 1 {
		res = res + "'"
	} else if len(r) >= 2 {
		res = string(r[:len(r)-1]) + "\"" + string(r[len(r)-1])
	}
	return res
}

func gematriyaDay(d int) string {
	if d <= 0 || d > 30 {
		return fmt.Sprintf("%d", d)
	}
	return intToHebrew(d)
}

func yearToHebrew(year int) string {
	if year >= 5000 {
		return "ה'" + intToHebrew(year-5000)
	}
	return intToHebrew(year)
}

func hdateToGreg(hd hdate.HDate) time.Time {
	y, m, d := hd.Greg()
	return time.Date(y, m, d, 12, 0, 0, 0, time.UTC)
}

//export GregorianToHebrew
func GregorianToHebrew(year, month, day C.int) *C.char {
	t := time.Date(int(year), time.Month(month), int(day), 12, 0, 0, 0, time.UTC)
	hd := hdate.FromTime(t)
	hm := hd.Month()
	hy := hd.Year()
	result := map[string]interface{}{
		"hebYear":     hy,
		"hebMonth":    int(hm),
		"hebDay":      hd.Day(),
		"monthName":   hebMonthNameHebrew(hm, hy),
		"monthNameEn": hm.String(),
		"render": fmt.Sprintf("%s ב%s %s", gematriyaDay(hd.Day()),
			hebMonthNameHebrew(hm, hy), yearToHebrew(hy)),
		"renderEn":  fmt.Sprintf("%d %s %d", hd.Day(), hm.String(), hy),
		"dayOfWeek": int(t.Weekday()),
	}
	return toCString(result)
}

//export HebrewToGregorian
func HebrewToGregorian(hebYear, hebMonth, hebDay C.int) *C.char {
	hd := hdate.New(int(hebYear), hdate.HMonth(int(hebMonth)), int(hebDay))
	t := hdateToGreg(hd)
	result := map[string]interface{}{
		"year":  t.Year(),
		"month": int(t.Month()),
		"day":   t.Day(),
	}
	return toCString(result)
}

//export GetMonthlyCalendar
func GetMonthlyCalendar(gregYear, gregMonth, useIsrael, noModern C.int) *C.char {
	firstDay := time.Date(int(gregYear), time.Month(gregMonth), 1, 12, 0, 0, 0, time.UTC)
	lastDay := firstDay.AddDate(0, 1, -1)

	opts := hebcal.CalOptions{
		Start:         hdate.FromTime(firstDay),
		End:           hdate.FromTime(lastDay),
		IL:            useIsrael != 0,
		Sedrot:        true,
		NoMinorFast:   false,
		NoModern:      noModern != 0,
		NoRoshChodesh: false,
	}

	events, err := hebcal.HebrewCalendar(&opts)
	if err != nil {
		return toCString(map[string]interface{}{"error": err.Error()})
	}

	eventsByDate := make(map[string][]map[string]interface{})
	for _, ev := range events {
		hd := ev.GetDate()
		g := hdateToGreg(hd)
		key := fmt.Sprintf("%04d-%02d-%02d", g.Year(), int(g.Month()), g.Day())

		// "he-x-NoNikud" gives plene Hebrew spelling without vowel marks.
		desc := ev.Render("he-x-NoNikud")
		if desc == "" {
			desc = ev.Render("he")
		}
		if desc == "" {
			desc = ev.Render("")
		}
		descEn := ev.Render("en")
		flags := int64(ev.GetFlags())

		evMap := map[string]interface{}{
			"desc":   desc,
			"descEn": descEn,
			"flags":  flags,
			"emoji":  ev.GetEmoji(),
			"isHoliday": (flags&int64(event.CHAG)) != 0 ||
				(flags&int64(event.YOM_TOV_ENDS)) != 0,
			"isMajor": (flags&int64(event.MAJOR_FAST)) != 0 ||
				(flags&int64(event.CHAG)) != 0,
			"isCandleLighting": (flags&int64(event.LIGHT_CANDLES)) != 0 ||
				(flags&int64(event.LIGHT_CANDLES_TZEIS)) != 0,
			"isHavdalah":    (flags & int64(event.YOM_TOV_ENDS)) != 0,
			"isParasha":     (flags & int64(event.PARSHA_HASHAVUA)) != 0,
			"isRoshChodesh": (flags & int64(event.ROSH_CHODESH)) != 0,
			"isFastDay": (flags&int64(event.MINOR_FAST)) != 0 ||
				(flags&int64(event.MAJOR_FAST)) != 0,
		}
		eventsByDate[key] = append(eventsByDate[key], evMap)
	}

	days := []map[string]interface{}{}
	current := firstDay
	for current.Month() == firstDay.Month() {
		hd := hdate.FromTime(current)
		hm := hd.Month()
		hy := hd.Year()
		key := fmt.Sprintf("%04d-%02d-%02d", current.Year(), int(current.Month()), current.Day())
		dayEvents := eventsByDate[key]
		if dayEvents == nil {
			dayEvents = []map[string]interface{}{}
		}

		days = append(days, map[string]interface{}{
			"gregYear":     current.Year(),
			"gregMonth":    int(current.Month()),
			"gregDay":      current.Day(),
			"hebYear":      hy,
			"hebMonth":     int(hm),
			"hebDay":       hd.Day(),
			"hebDayStr":    gematriyaDay(hd.Day()),
			"hebMonthName": hebMonthNameHebrew(hm, hy),
			"dayOfWeek":    int(current.Weekday()),
			"events":       dayEvents,
		})
		current = current.AddDate(0, 0, 1)
	}

	return toCString(map[string]interface{}{
		"year":  int(gregYear),
		"month": int(gregMonth),
		"days":  days,
	})
}

//export GetZmanim
func GetZmanim(gregYear, gregMonth, gregDay C.int, lat, lon, elev C.double, tz *C.char) *C.char {
	tzName := C.GoString(tz)
	loc, err := time.LoadLocation(tzName)
	if err != nil {
		loc = time.UTC
	}
	_ = elev // hebcal-go v0.11.1's zmanim.Location does not accept elevation

	date := time.Date(int(gregYear), time.Month(gregMonth), int(gregDay), 12, 0, 0, 0, loc)

	geoLoc := &zmanim.Location{
		Name:       "",
		Latitude:   float64(lat),
		Longitude:  float64(lon),
		TimeZoneId: tzName,
	}

	z := zmanim.New(geoLoc, date)

	fmt24 := func(t time.Time) string {
		if t.IsZero() {
			return ""
		}
		return t.In(loc).Format("15:04")
	}

	result := map[string]interface{}{
		"alotHaShachar":     fmt24(z.AlotHaShachar()),
		"misheyakir":        fmt24(z.Misheyakir()),
		"misheyakirMachmir": fmt24(z.MisheyakirMachmir()),
		"sunrise":           fmt24(z.Sunrise()),
		"sofZmanShmaMGA":    fmt24(z.SofZmanShmaMGA()),
		"sofZmanShma":       fmt24(z.SofZmanShma()),
		"sofZmanTfillaMGA":  fmt24(z.SofZmanTfillaMGA()),
		"sofZmanTfilla":     fmt24(z.SofZmanTfilla()),
		"chatzot":           fmt24(z.Chatzot()),
		"minchaGedola":      fmt24(z.MinchaGedola()),
		"minchaKetana":      fmt24(z.MinchaKetana()),
		"plagHaMincha":      fmt24(z.PlagHaMincha()),
		"sunset":            fmt24(z.Sunset()),
		"tzeit":             fmt24(z.Tzeit(8.5)),
		"tzeit72":           fmt24(z.Sunset().Add(72 * time.Minute)),
		"candleLighting18":  fmt24(z.Sunset().Add(-18 * time.Minute)),
	}
	return toCString(result)
}

//export GetTodayHebrewDate
func GetTodayHebrewDate(tz *C.char) *C.char {
	tzName := C.GoString(tz)
	loc, err := time.LoadLocation(tzName)
	if err != nil {
		loc = time.Local
	}
	now := time.Now().In(loc)
	hd := hdate.FromTime(now)
	hm := hd.Month()
	hy := hd.Year()

	result := map[string]interface{}{
		"day":       hd.Day(),
		"dayStr":    gematriyaDay(hd.Day()),
		"month":     int(hm),
		"monthName": hebMonthNameHebrew(hm, hy),
		"year":      hy,
		"yearStr":   yearToHebrew(hy),
		"short":     fmt.Sprintf("%s ב%s", gematriyaDay(hd.Day()), hebMonthNameHebrew(hm, hy)),
	}
	return toCString(result)
}

//export GetUpcomingShabbat
func GetUpcomingShabbat(lat, lon, elev C.double, tz *C.char, useIsrael, candleLightingMinutes C.int) *C.char {
	tzName := C.GoString(tz)
	loc, err := time.LoadLocation(tzName)
	if err != nil {
		loc = time.UTC
	}

	now := time.Now().In(loc)
	daysToFriday := (int(time.Friday) - int(now.Weekday()) + 7) % 7
	if daysToFriday == 0 && now.Hour() >= 18 {
		daysToFriday = 7
	}
	friday := now.AddDate(0, 0, daysToFriday)
	saturday := friday.AddDate(0, 0, 1)

	parasha := ""
	hd := hdate.FromTime(saturday)
	opts := hebcal.CalOptions{
		Start:  hd,
		End:    hd,
		IL:     useIsrael != 0,
		Sedrot: true,
	}
	evs, err := hebcal.HebrewCalendar(&opts)
	if err == nil {
		for _, ev := range evs {
			if (int64(ev.GetFlags()) & int64(event.PARSHA_HASHAVUA)) != 0 {
				parasha = ev.Render("he-x-NoNikud")
				if parasha == "" {
					parasha = ev.Render("he")
				}
				if parasha == "" {
					parasha = ev.Render("")
				}
				break
			}
		}
	}

	geoLoc := &zmanim.Location{
		Latitude:   float64(lat),
		Longitude:  float64(lon),
		TimeZoneId: tzName,
	}
	_ = elev // hebcal-go v0.11.1's zmanim.Location does not accept elevation
	zFri := zmanim.New(geoLoc, friday)
	zSat := zmanim.New(geoLoc, saturday)

	candleMin := time.Duration(int(candleLightingMinutes)) * time.Minute
	candleLight := zFri.Sunset().Add(-candleMin)
	havdalah := zSat.Tzeit(8.5)

	// Returns only HH:MM; the C# side composes any user-facing date text using
	// the fridayDate/saturdayDate ISO strings below.
	locFmt := func(t time.Time) string {
		if t.IsZero() {
			return ""
		}
		return t.In(loc).Format("15:04")
	}

	result := map[string]interface{}{
		"parasha":        parasha,
		"candleLighting": locFmt(candleLight),
		"havdalah":       locFmt(havdalah),
		"fridayDate":     friday.Format("2006-01-02"),
		"saturdayDate":   saturday.Format("2006-01-02"),
	}
	return toCString(result)
}
