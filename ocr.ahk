;Credits for OCR: teadrinker for basic script and malcev for the advanced scripts
;Credits for the screen selection: Joe Glines
;I changed few things from them to make them work better for me.
#SingleInstance, Force 
#UseHook
ListLines Off
Process, Priority, , A
SetBatchLines, -1
SetWinDelay, -1
SetControlDelay, -1
AutoTrim, On
DetectHiddenWindows, On
SetWorkingDir %A_ScriptDir%
CoordMode, Mouse, Screen
;#include %A_ScriptDir%\lib\Gdip_All.ahk
;#Include %A_ScriptDir%\lib\subtitle.ahk
;#Include %A_ScriptDir%\lib\Gdip_All.ahk
;GUI

global showGuild := 
global showClass := 
global showLevel := 
global showRR := 
global showSoloKills := 
global showResists := 
global showEvent := 
global showTimer := 
global showccTimer := 
global SliderText := 
global ResistsSliderText := 
global TimerSliderText := 
global ccTimerSliderText := 
global name := ""
global serverName := 
global pToken := Gdip_Startup()
global initDone := false
if pToken =
{
	MsgBox, 64, GDI+ error, GDI+ failed to start. Please ensure you have GDI+ on your system.
	return
}
global overlayText := {}
overlayText.font := "Gill Sans MT Condensed"
overlayText.size := 20
overlayText.outline := {}
overlayText.outline.stroke := "2 px"
overlayText.outline.color := "Black"
overlayText.color := "White"

global overlayBackground := {}
overlayBackground.color := "Transparent"
overlayBackground.x := "center"
overlayBackground.y := "40vh"


global overlayTextTimer := {}
overlayTextTimer.font := "Gill Sans MT Condensed"
overlayTextTimer.size := 20
overlayTextTimer.outline := {}
overlayTextTimer.outline.stroke := "2 px"
overlayTextTimer.outline.color := "Black"
overlayTextTimer.color := "White"

global overlayBackgroundTimer := {}
overlayBackgroundTimer.color := "Transparent"
overlayBackgroundTimer.x := "center"
overlayBackgroundTimer.y := "40vh"


global overlayTextccTimer := {}
overlayTextccTimer.font := "Gill Sans MT Condensed"
overlayTextccTimer.size := 20
overlayTextccTimer.outline := {}
overlayTextccTimer.outline.stroke := "2 px"
overlayTextccTimer.outline.color := "Black"
overlayTextccTimer.color := "White"


global overlayBackgroundccTimer := {}
overlayBackgroundccTimer.color := "Transparent"
overlayBackgroundccTimer.x := "center"
overlayBackgroundccTimer.y := "40vh"

global overlayResists :=  {}
overlayResists.font := "Gill Sans MT Condensed"
overlayResists.size := 20
overlayResists.outline := {}
overlayResists.outline.stroke := "2 px"
overlayResists.outline.color := "Black"
overlayResists.color := "White"

global overlayBackgroundResists := {}
overlayBackgroundResists.color := "Transparent"
overlayBackgroundResists.x := "center"
overlayBackgroundResists.y := "40vh"

global pBitmap :=
global hBitmap :=
global texts := 
global rows := []
global html := 
global htmlLines := 
global guiiInfoArray := {cName: "Herald", cGuild: "Helper", cClass: "Gamemaster", cLevel: "99", cRankLevel: "R13L0", cSolo: "99,999"}
global edenClassList := {59: "Warlock", 58: "Vampiir", 63: "Occultist",2: "Armsman", 11: "Mercenary", 1: "Paladin", 19: "Reaver", 13: "Cabalist", 12: "Necromancer", 8: "Sorcerer", 5: "Theurgist", 7: "Wizard", 4: "Minstrel", 6: "Cleric", 10: "Friar", 9: "Infiltrator", 3: "Scout",33: "Herectic" ,43: "Blademaster", 45: "Champion", 44: "Hero", 56: "Valewalker", 55: "Animist", 40: "Eldritch", 41: "Enchanter", 42: "Mentalist", 48: "Bard", 47: "Druid", 46: "Warden", 49: "Nightshade",39: "Bainshee" ,50: "Ranger", 31: "Berserker", 32: "Savage", 24: "Skald", 21: "Thane", 22: "Warrior", 30: "Bonedancer", 29: "Runemaster", 27: "Spiritmaster", 26: "Healer", 28: "Shaman", 25: "Hunter", 23: "Shadowblade", 34: "Valkyrie"} ;*[ocr]
global edenRaceList := {1: "Briton", 2: "Avalonian", 3: "Highlander", 4: "Saracen", 5: "Norseman", 6: "Troll", 7: "Dwarf", 8: "Kobold", 9: "Celt", 10: "Firbolg", 11: "Elf", 12: "Lurikeen", 13: "Inconnu", 14: "Valkyn", 15: "Sylvan", 16: "Half Ogre", 17: "Frostalf", 18: "Shar", 19: "Korazh", 20: "Deifrang", 21: "Graoch"}
global edenRPList := [0,25,125,350,750,1375,2275,3500,5100,7125,9625,12650,16250,20475,25375,31000,37400,44625,52725,61750,71750,82775,94875,108100,122500,138125,155025,173250,192850,213875,236375,260400,286000,313225,342125,372750,405150,439375,475475,513500,553500,595525,639625,685850,734250,784875,837775,893000,950600,1010625,1073125,1138150,1205750,1275975,1348875,1424500,1502900,1584125,1668225,1755250,1845250,1938275,2034375,2133600,2236000,2341625,2450525,2562750,2678350,2797375,2919875,3045900,3175500,3308725,3445625,3586250,3730650,3878875,4030975,4187000,4347000,4511025,4679125,4851350,5027750,5208375,5393275,5582500,5776100,5974125,6176625,6383650,6595250,6811475,7032375,7258000,7488400,7723625,7963725,8208750,9111713,10114001,11226541,12461460,13832221,15353765,17042680,18917374,20998286,23308097,25871988,28717906,31876876,35383333,39275499,43595804,48391343,53714390,59622973,66181501,73461466,81542227,90511872,100468178,111519678,123786843,137403395,152517769,169294723,187917143]
global edenXPList := [0,50,250,862,2452,6566,17264,42969,104624,252597,578184,1084636,1872442,3097862,5004071,7969199,12581627,19756017,30915510,47879943,73669337,108596860,155900432,219965478,306731040,424247833,583405025,798957443,1090888054,1473042173,1983893518,2659707500,3553701950,4736314134,6300721436,8370183229,10954252985,14180710266,18209249899,23239265725,29519719603,37361918369,47153651218,59379562666,74644778569,93704855776,117504553416,147220744543,184324241109,230651492549,4999999999950]
;global edenXPList := [[1, 0], [2, 50], [3, 250], [4, 862], [6, 2452],[7,6566],[8,17264],[9,42969],[10,104624],[11,252597],[12,578184],[12,1084636],[13,1872442],[14,3097862],[15,5004071],[16,7969199],[17,12581627],[18,19756017],[19,30915510],[20,47879943],[21,73669337],[22,108596860],[23,155900432],[24,219965478],[25,306731040],[26,424247833],[27,583405025],[28,798957443],[29,1090888054],[30,1473042173],[31,1983893518],[32,2659707500],[33,3553701950],[34,4736314134],[35,6300721436],[36,8370183229],[37,10954252985],[38,14180710266],[39,18209249899],[40,23239265725],[41,29519719603],[42,37361918369],[43,47153651218],[44,59379562666],[45,74644778569],[46,93704855776],[47,117504553416],[48,147220744543],[49,184324241109],[50,230651492549]]
global charName := 
global charGuild := 
global charClass := 
global charLevel := 
global charRankLevel := 
global charRace := 
global charRankName := 
global soloKills := 
global guiiSpace := " "
global guiiNewLine := "`n"
global guiiTab := "       "
global guiiInfo := 
global guiiInfoMezzTime := 
global guiiInfoRootTime := 
global guiiInfoStunTime := 
global checkIfMainPage :=
global r := 
global riid := 
global res := 
global IID_IRandomAccessStream := "{905A0FE1-BC53-11DF-8C49-001E4FC686DA}", IID_IPicture:= "{7BF80980-BF32-101A-8BBB-00AA00300CAB}", PICTYPE_BITMAP := 1, BSOS_DEFAULT   := 0
global racesAlb := "Avalonian Briton Half Ogre Highlander Inconnu Saracen"
global racesMid := "Dwarf Frostalf Kobold Norseman Troll Valkyn"
global racesHib := "Celt Elf Firbolg Lurikeen Shar Sylvan"
global classesAlb := "Paladin Armsman Armswoman Infiltrator Friar Cleric Mercenary Minstrel Reaver Scout Wizard Theurgist Cabalist Necromancer Sorcerer Sorceress Heretic MaulerAlb"
global classesMid := "Healer Shaman Skald Thane Warrior Berserker Hunter Savage Shadowblade Bonedancer Runemaster Spiritmaster Valkyrie Warlock MaulerMid"
global classesHib := "Valewalker Animist Eldritch Enchanter Enchantress Mentalist Bard Blademaster Ranger Nightshade Champion Heroine Hero Druid Warden Bainshee Vampiir MaulerHib"
global lightDet := "Berserker Skald Savage Thane Shadowblade Hunter Reaver Friar Paladin Infiltrator Scout Champion Warden Nightshade Ranger Valewalker"
global det := "Warrior Mercenary Armswoman Armsman Blademaster Hero Heroine Valkyrie"

global PLATELEATHER = "Paladin Armsman Armswoman Infiltrator Friar"
global SCALE = "Champion Heroine Hero Druid Warden"
global CHAINSTUDDED = "Cleric Mercenary Minstrel Reaver Scout"
global CHAINMID = "Healer Shaman Skald Thane Warrior Valkyrie"
global REINFORCEDLEAHTER = "Bard Blademaster Ranger Nightshade Vampiir"
global STUDDEDLEATHER = "Berserker Hunter Savage Shadowblade"
global CLOTHALB = "Wizard Theurgist Cabalist Necromancer Sorcerer Sorceress"
global CLOTHMID = "Bonedancer Runemaster Spiritmaster"
global CLOTHHIB = "Valewalker Animist Eldritch Enchanter Enchantress Mentalist"

global fileVar :=
global checkName := 
global MX := 
global MY := 
global w := 
global h := 
global resis := 0
global edenHeraldCookie := 
global edenHeraldCookieRead := 
global edenHeraldUserAgent := 
global cfgFont := "Gill Sans MT Condensed"

global timerInformation := [[]]
global abilities := [[]]
global guiiTimerInfo := 
global timerInformationEmpty := true
global timerOff := true
global removeElements := []
global area :=

global GREEN := "00FF00"
global RED := "FF0909"
global WHITE := "FFFFFF"
global thrust := "Thrust"
global slash := "Slash"
global crush := "Crush"
global colorOrder := []

Gui, font, s7, Verdana Bold
Gui, +LastFound
Gui, -Caption -Border +ToolWindow +AlwaysOnTop
Gui, Margin, 1, 1
;Buttons
Gui, Add, Button, gMoveOverlay, Move Overlay
Gui, Add, Button, x+18 gMoveTimer, Move Timer
Gui, Add, Button, x+18 gMoveccTimer, Move ccTimer
Gui, Add, Button, x+18 gMoveResists, Move Resists
readConfig()
;TextSlider
Gui, Add, text, xs, Font Size:
Gui, Add, Slider, w245 x+18 vFontSlider gFontSlider Range1-100 AltSubmit, % overlayText.size
Gui, Add, text,	w20 x+1 vSliderText, % overlayText.size

Gui, Add, text, xs, Resists Size:
Gui, Add, Slider, w245 x+5 vResistsSlider gResistsSlider Range1-100 AltSubmit, % overlayResists.size 
Gui, Add, text,	w20 x+1 vResistsSliderText, % overlayResists.size

Gui, Add, text, xs, Timer Size:
Gui, Add, Slider, w245 x+13 vTimerSlider gTimerSlider Range1-100 AltSubmit, % overlayTextTimer.size
Gui, Add, text,	w20 x+1 vTimerSliderText, % overlayTextTimer.size

Gui, Add, text, xs, ccTimer Size:
Gui, Add, Slider, w245 x+3 vccTimerSlider gccTimerSlider Range1-100 AltSubmit, % overlayTextccTimer.size
Gui, Add, text,	w20 x+1 vccTimerSliderText, % overlayTextccTimer.size

;Checkboxes
If(showGuild)
	Gui, Add,CheckBox,xs Checked vshowGuild gShowGuild, Guild
else
	Gui, Add,CheckBox,xs vshowGuild gShowGuild, Guild

If(showClass)
	Gui, Add,CheckBox,x+0 Checked vshowClass gShowClass, Class
else
	Gui, Add,CheckBox,x+0 vshowClass gShowClass, Class

If(showLevel)
	Gui, Add,CheckBox,x+0 Checked vshowLevel gShowLevel, Level
else
	Gui, Add,CheckBox,x+0 vshowLevel gShowLevel, Level

If(showRR)
	Gui, Add,CheckBox,x+0 Checked vshowRR gShowRR, RR
else
	Gui, Add,CheckBox,x+0 vshowRR gShowRR, RR

If(showSoloKills)
	Gui, Add,CheckBox,x+0 Checked vshowSoloKills gShowSoloKills, Solo
else
	Gui, Add,CheckBox,x+0 vshowSoloKills gShowSoloKills, Solo

If(showResists)
	Gui, Add,CheckBox,x+0 Checked vshowResists gShowResists, Resists
else
	Gui, Add,CheckBox,x+0 vshowResists gShowResists, Resists

If(showEvent)
	Gui, Add,CheckBox,x+0 Checked vshowEvent gshowEvent, Event
else
	Gui, Add,CheckBox,x+0 vshowEvent gshowEvent, Event

If(showTimer)
	Gui, Add,CheckBox,xs Checked vshowTimer gshowTimer, Show Timer
else
	Gui, Add,CheckBox,xs vshowTimer gshowTimer, Show Timer

If(showccTimer)
	Gui, Add,CheckBox,x+0 Checked vshowccTimer gshowccTimer, Show ccTimer
else
	Gui, Add,CheckBox,x+0 vshowccTimer gshowccTimer, Show ccTimer

Gui, Add, Button,xs w355 gSaveConfig, Save Config
Gui, Add, Button,w355 gcloseSettings, Close Settings
OnMessage( 0x200, "WM_MOUSEMOVE" )

WM_MOUSEMOVE( wparam, lparam, msg, hwnd )
{
	if wparam = 1 ; LButton
		PostMessage, 0xA1, 2,,, A ; WM_NCLBUTTONDOWN
}
Gui, Show, AutoSize Center
Gui, Submit, NoHide
global guii := new Subtitle().ClickThrough()
global guiiTimer := new Subtitle().ClickThrough()
global guiiccTimer := new Subtitle().ClickThrough()
global guiiResists := new Subtitle().ClickThrough()

init()
return

;GUI
ShowGuild:
showGuild := !showGuild
guiiInfoDraw()
return
ShowClass:
showClass := !showClass
guiiInfoDraw()
return
ShowLevel:
showLevel := !showLevel
guiiInfoDraw()
return
ShowRR:
showRR := !showRR
guiiInfoDraw()
return
ShowSoloKills:
showSoloKills := !showSoloKills
guiiInfoDraw()
return
ShowResists:
showResists := !showResists
guiiInfoDraw()
return

ShowEvent:
showEvent := !showEvent
return

ShowTimer:
showTimer := !showTimer
return

ShowccTimer:
showccTimer := !showccTimer
return

SaveConfig:
writeConfig()
return

MoveOverlay:
setLocationOverlay()
return

MoveTimer:
setLocationTimerOverlay()
return

MoveccTimer:
setLocationccTimerOverlay()
return

MoveResists:
moveResists()
return

closeSettings:
Gui, Hide
return

FontSlider:
Gui,Submit,NoHide
GuiControl,, SliderText, %FontSlider%
overlayText.size := FontSlider
guiiInfoDraw()
return

ResistsSlider:
Gui,Submit,NoHide
GuiControl,, ResistsSliderText, %ResistsSlider%
overlayResists.size := ResistsSlider
guiiInfoDraw()
return

TimerSlider:
Gui,Submit,NoHide
GuiControl,, TimersliderText, %Timerslider%
overlayTextTimer.size := Timerslider
guiiInfoDraw()
return

ccTimerSlider:
Gui,Submit,NoHide
GuiControl,, ccTimersliderText, %ccTimerslider%
overlayTextccTimer.size := ccTimerslider
guiiInfoDraw()
return

;WM_LBUTTONDOWN(wParam, lParam, msg, hwnd)
;{
;	Gui, +LastFound
;	PostMessage, 0xA1, 2 ;0xA1 is WM_NCLBUTTONDOWN, to make Windows think we clicked on the non-client area of the window (the border).  The "2" tells windows we clicked on caption at the top of the window, as if to drag it.
;} 


EmptyMem(PID="ocr"){
	pid:=(pid="ocr") ? DllCall("GetCurrentProcessId") : pid
	h:=DllCall("OpenProcess", "UInt", 0x001F0FFF, "Int", 0, "Int", pid)
	DllCall("SetProcessWorkingSetSize", "UInt", h, "Int", -1, "Int", -1)
	DllCall("CloseHandle", "Int", h)
}
^!Lbutton::dragWindow() ; CTRL+ALT+LEFTMOUSE Create Window to read Chat
~!LWin::showGui() ; ALT+WIN Open settings
;*^Lbutton::setLocationOverlay() ; CTRL+ALT+LEFTMOUSE Move the Overlay to mouse click
;^!Rbutton::setLocationTimerOverlay() ; CTRL+ALT+RIGHTMOUSE Move the TimerOverlay to mouse click
;^!MButton::setLocationccTimerOverlay() ; CTRL+ALT+MIDMOUSE Myove the ccTimerOverlay to mouse click
;^!x::writeConfig() ; CTRL+ALT+X Save config
^!WheelUp::changeTextSize(true) ; CTRL+ALT+MWHEELUP Increase Overlay font size
^!WheelDown::changeTextSize(false) ; CTRL+ALT+MWHEELUP Decrease Overlay font size

showGui()
{
	Gui, Show
}

updateFontOverlay()
{
	
	overlayText.font := cfgFont
	
	overlayTextTimer.font := cfgFont
	
	overlayTextccTimer.font := cfgFont
	
	overlayResists.font := cfgFont
	
	guiiInfoDraw()
}

DebugMessage(debugText)
{
	DebugWindow("---------------`n")
	DebugWindow(debugText)
	DebugWindow("`n---------------`n")
}

removeToTimerInformation(name, abType)
{
	for i, element in timerInformation
	{
		if(element.tName == name && element.tType == abType)
		{
			timerInformation.RemoveAt(i)
			break
		}
	}
}


addToTimerInformation(name, skill, ccLength, abType)
{
	if timerInformationEmpty && timerOff
	{
		SetTimer, TimerDecrease, 1000
		timerOff := false
		timerInformationEmpty := false
	}
	
	if timerInformationEmpty && !timerOff
	{
		SetTimer, TimerDecrease, off
		timerOff := true
		timerInformationEmpty := true
	}
	notIn := true
	
	for i, element in timerInformation
	{
		if(element.tName == name && element.tType == abType)
		{
			notIn := false
			break
		}
	}
	if(skill == "m")
	{
		ccLengthModifier := 6
	} else
	{
		ccLengthModifier := 10
		
		if InStr(det, guiiInfoArray.cClass) != 0
		{
			ccLength := Floor(ccLength*(0.74-(resis/100))*0.2)
		}
		else if InStr(lightDet, guiiInfoArray.cClass) != 0
		{
			ccLength := Floor(ccLength*(0.74-(resis/100))*0.45)
		}
		else
			ccLength := Floor(ccLength*(0.74-(resis/100)))
		
		
	}
	
	
	ccTotalLength := ccLength*ccLengthModifier
	
	if(ccTotalLength >= 60 || skill == "s")n
	{
		ccTotalLength := 60 + ccLength
	}
	else
	{
		ccTotalLength := ccTotalLength + ccLength
	}
	
	if(abType == "m" && notIn)
	{
		timerInformation.Push({tName: name, tType: abType, tTime: ccTotalLength})
		SetTimer, TimerRemoveLast, 1000
	}
	
	if(abType == "s" && notIn)
	{
		timerInformation.Push({tName: name, tType: abType, tTime: ccTotalLength})
		SetTimer, TimerRemoveLast, 1000
	}
	
	if(abType == "r" && notIn)
	{
		timerInformation.Push({tName: name, tType: abType, tTime: ccTotalLength})
		SetTimer, TimerRemoveLast, 1000
	}
}


drawTimerInformation()
{
	if(showTimer)
	{
		guiiTimerInfo := 
		for i, element in timerInformation
		{
			guiiTimerInfo := 
			Loop % i
			{
				guiiTimerInfo .= guiiNewLine
			}
			guiiTimerInfo .= element.tName
			guiiTimerInfo .= guiiSpace
			tt := element.tTime
			guiiTimerInfo = %guiiTimerInfo%%tt%
			guiiTimerInfo .= guiiNewLine
			
			if(element.tType == "m")
			{
				overlayTextTimer.color := "dcd335"
			}
			if(element.tType == "s")
			{
				overlayTextTimer.color := "cf33a4"
			}
			if(element.tType == "r")
			{
				overlayTextTimer.color := "a87130"
			}
			guiiTimer.draw(guiiTimerInfo, overlayBackgroundTimer, overlayTextTimer)
			
		}
	}
	guiiTimer.Render()
}

TimerDecrease:
for i, element in timerInformation
{
	
	if(element.tTime == 1)
	{
		element.tTime := element.tTime-1
		removeElements.Push(i)
		if(element.tType == "m")
		{
			guiiInfoMezzTime := 
		}
		
		if(element.tType == "s")
		{
			guiiInfoStunTime := 
		}
		
		if(element.tType == "r")
		{
			guiiInfoRootTime := 
		}
	}
	else
	{
		element.tTime := element.tTime-1
	}
	if(name == element.tName)
	{
		if(element.tType == "m")
		{
			guiiInfoMezzTime := element.tTime
		}
		
		if(element.tType == "s")
		{
			guiiInfoStunTime := element.tTime
		}
		
		if(element.tType == "r")
		{
			guiiInfoRootTime := element.tTime
		}
	}
	
	if(i == timerInformation.MaxIndex())
	{
		for j, k in removeElements
		{
			timerInformation.RemoveAt(k-j+1)
			
		}
		removeElements := []
	}
	
	guiiInfoDraw()
}
return

global removeCounter := 0

TimerRemoveLast:
if(removeCounter < 2)
{
	removeCounter := removeCounter+1
	resisted := InStr(texts, "resists")
	cancelled := InStr(texts, "cancelled")
	again := InStr(texts, "again")
	if(resisted+cancelled+again)
	{
		timerInformation.RemoveAt(i)
		SetTimer, TimerRemoveLast, off
		removeCounter := 0
	}
	else
	{
		SetTimer, TimerRemoveLast, off
		removeCounter := 0
	}
}
return

global placeName :=
global lastName := 

TargetFound := false
MemberFound := false


readOutText()
{
	pToken := Gdip_Startup()
	timerInformation.Pop(0)
	doubleTarget := false
	;streamOverlay()
	loop
	{
		;IfWinActive, ahk_class DAoCMWC
		;{
		if(area != "")
		{
			pBitmap := Gdip_BitmapFromScreen(area)
			hBitmap := Gdip_CreateHBITMAPFromBitmap(pBitmap) ;Convert an hBitmap from the pBitmap
			pIRandomAccessStream := HBitmapToRandomAccessStream(hBitmap) ;OCR function needs a randome access stream (so it isn't "locked down")
			texts := ocr(pIRandomAccessStream)
			Gdip_DisposeImage(pBitmap)
			DeleteObject(pBitmap)
			DeleteObject(hBitmap)
			ObjRelease(pIRandomAccessStream)
		}
		;DebugMessage(texts)
		loop, parse, texts, `n
		{
			;DebugMessage(texts) ;*[ocr]
			if RegExMatch(a_loopfield, "You target \[(.*)\]", nn)
			{
				lastName := nn1
				StringReplace, lastName, lastName, %A_SPACE%,, All
				TargetFound := true
				if(MemberFound)
				{
					MemberFound := false
				}
			}
			if(TargetFound == true && RegExMatch(a_loopfield, "member", nn))
			{
				MemberFound := true
			}
		}
		
		
		
		;calc := new _ClassMemory("ahk_exe eden.dll", "", hProcessCopy) 
		;if !isObject(calc) 
		;{
			;msgbox failed to open a handle
			;if (hProcessCopy = 0)
				;msgbox The program isn't running (not found) or you passed an incorrect program identifier parameter. In some cases _ClassMemory.setSeDebugPrivilege() may be required. 
			;else if (hProcessCopy = "")
				
			;msgbox OpenProcess failed. If the target process has admin rights, then the script also needs to be ran as admin. _ClassMemory.setSeDebugPrivilege() may also be required. Consult A_LastError for more information.
			;ExitApp
		;}
		;addressOfString := 0x377403D4 + 0x4
		;lastName := calc.readString(addressOfString, 100, "CP0")
		;DebugMessage(lastName)
		
		
		
		if(name != lastName && MemberFound or name != lastName && showEvent)
		{
			guiiInfoMezzTime := 
			guiiInfoStunTime := 
			guiiInfoRootTime := 
			name := lastName
			getHeraldInfo()
			;DebugMessage(lastName)
		}
		TargetFound := false
		MemberFound := false
		
		
		if(name != "")
		{
			targetInString := InStr(texts, lastName)
			resisted := InStr(texts, "resists")
			cancelled := InStr(texts, "cancelled")
			again := InStr(texts, "again")
			for i, element in abilities
			{
				;if(InStr(texts,element.abilityName) > targetInString && InStr(texts,element.abilityName) > resisted+cancelled+again)
				if(InStr(texts,element.abilityName) > resisted+cancelled+again && InStr(texts,lastName) < InStr(texts,element.abilityName))
				{
					addToTimerInformation(lastName, element.abilitySkill, element.abilityTime, element.abType)
				}
				else if(InStr(texts,lastName) && InStr(texts,element.abilityName) > resisted+cancelled+again && InStr(texts,lastName) < InStr(texts,element.abilityName))
				{
					removeToTimerInformation(lastName, element.abType)
				}
			}
			;agility := InStr(texts, "agility returns")
			;health := InStr(texts, "health returns")
			;strength := InStr(texts, "strength returns")
		}
		EmptyMem() ;Clear Ram usage every new target
		;}
	}
	DeleteObject(output)
	DeleteObject(texts)
	DeleteObject(ErrorLevel)
	texts := q
	output := ""
	Gdip_Shutdown(pToken)
	
}


dragWindow()
{	
	area := SCW_SelectAreaMod("g" GuiNum " c" SelColor " t" SelTrans)
	StringSplit, v, area, |
}

init()
{ ;*[ocr]
	overlayBackground.x := (A_ScreenWidth // 2)
	overlayBackground.y := (A_ScreenHeight // 2)
	overlayBackgroundTimer.x := (A_ScreenWidth // 2)
	overlayBackgroundTimer.y := (A_ScreenHeight // 2)
	overlayBackgroundccTimer.x := (A_ScreenWidth // 2)
	overlayBackgroundccTimer.y := (A_ScreenHeight // 2)
	overlayResists.x := (A_ScreenWidth // 2)
	overlayResists.y := (A_ScreenHeight // 2)
	initDone := true
	readConfig()
	if(MX != "" and MY != "" and w != "" and h != "")
	{
		area := MX "|" MY "|" w "|" h
		StringSplit, v, area, |
	}
	updateFontOverlay()	
	guiiInfoDraw()
	readOutText()
}

readConfig()
{
	
	FileRead, fileVar, cfg.ini
	if RegExMatch(fileVar, "overlayX:(.*)", s)
	{
		overlayBackground.x := s1
	}
	if RegExMatch(fileVar, "overlayY:(.*)", s)
	{
		overlayBackground.y := s1
	}
	if RegExMatch(fileVar, "overlayXTimer:(.*)", s)
	{
		overlayBackgroundTimer.x := s1
	}
	if RegExMatch(fileVar, "overlayYTimer:(.*)", s)
	{
		overlayBackgroundTimer.y := s1
	}
	if RegExMatch(fileVar, "overlayXccTimer:(.*)", s)
	{
		overlayBackgroundccTimer.x := s1
	}
	if RegExMatch(fileVar, "overlayYccTimer:(.*)", s)
	{
		overlayBackgroundccTimer.y := s1
	}
	if RegExMatch(fileVar, "overlayXResists:(.*)", s)
	{
		overlayResists.x := s1
	}
	if RegExMatch(fileVar, "overlayYResists:(.*)", s)
	{
		overlayResists.y := s1
	}
	if RegExMatch(fileVar, "fontSize:(.*)", s)
	{
		if(s1 >= 8 or s1 <=100)
		{
			overlayText.size := s1
		}	
	}
	if RegExMatch(fileVar, "timerSize:(.*)", s)
	{
		if(s1 >= 8 || s1 <=100)
		{
			overlayTextTimer.size := s1
		}
	}
	if RegExMatch(fileVar, "ccSize:(.*)", s)
	{
		if(s1 >= 8 or s1 <=100)
		{
			overlayTextccTimer.size := s1
		}
	}
	if RegExMatch(fileVar, "resisSize:(.*)", s)
	{
		if(s1 >= 8 or s1 <=100)
		{
			overlayResists.size := s1
		}
	}
	if RegExMatch(fileVar, "MX:(.*)", s)
	{
		MX := s1
	}
	if RegExMatch(fileVar, "MY:(.*)", s)
	{
		MY := s1
	}
	if RegExMatch(fileVar, "w:(.*)", s)
	{
		w := s1
	}
	if RegExMatch(fileVar, "h:(.*)", s)
	{
		h := s1
	}
	if RegExMatch(fileVar, "show:(.*)", s)
	{
		if(StrLen(s1) == 9)
		{
			showGuild := SubStr(s1,1,1)
			showClass := SubStr(s1,2,1)
			showLevel := SubStr(s1,3,1)
			showRR := SubStr(s1,4,1)
			showSoloKills := SubStr(s1,5,1)
			showResists := SubStr(s1,6,1)
			showEvent := SubStr(s1,7,1)
			showTimer := SubStr(s1,8,1)
			showccTimer := SubStr(s1,9,1)
		}
	}
	if RegExMatch(fileVar, "server:(.*)", s)
	{
		serverName := s1
	}
	if RegExMatch(fileVar, "edenHeraldCookie:(.*)", s)
	{
		edenHeraldCookieRead := s1
		edenHeraldCookie := "eden_daoc_sid=" + s1
	}
	if RegExMatch(fileVar, "edenHeraldUserAgent:(.*)", s)
	{
		edenHeraldUserAgent := s1
	}
	if RegExMatch(fileVar, "font:(.*)", s)
	{
		cfgFont := s1
	}
	
	DeleteObject(fileVar)
	Loop, read, abilities.txt
	{
		if RegExMatch(A_LoopReadLine, "(.*)#(.*)#(.*)#(.*)", s)
		{
			abilities.Push({abilityName: s1, abilitySkill: s2, abilityTime: s3, abType: s4})
		}
	}
}

writeCharacter()
{
	;fileWriteCharacter := FileOpen("character.txt","w")
	fileVar := "Last target:`n"
	fileVar .= charClass
	fileVar .= " "
	fileVar .= charLevel
	fileVar .= " "
	fileVar .= charRankLevel
	;GuiControl,,Var,%fileVar%
	GuiControl, Text, Var, %fileVar%
	;fileWriteCharacter.write(fileVar)
	;fileWriteCharacter.close()
	DeleteObject(fileVar)
	;DeleteObject(fileWriteCharacter)
}

writeConfig()
{
	FileDelete, cfg.ini
	fileVar := ""
	fileVar .= "overlayX:"
	fileVar .= overlayBackground.x
	fileVar .= "`n"
	fileVar .= "overlayY:"
	fileVar .= overlayBackground.y
	fileVar .= "`n"
	fileVar .= "overlayXTimer:"
	fileVar .= overlayBackgroundTimer.x
	fileVar .= "`n"
	fileVar .= "overlayYTimer:"
	fileVar .= overlayBackgroundTimer.y
	fileVar .= "`n"
	fileVar .= "overlayXccTimer:"
	fileVar .= overlayBackgroundccTimer.x
	fileVar .= "`n"
	fileVar .= "overlayYccTimer:"
	fileVar .= overlayBackgroundccTimer.y
	fileVar .= "`n"
	fileVar .= "overlayXResists:"
	fileVar .= overlayResists.x
	fileVar .= "`n"
	fileVar .= "overlayYResists:"
	fileVar .= overlayResists.y
	fileVar .= "`n"
	fileVar .= "fontSize:"
	fileVar .= overlayText.size
	fileVar .= "`n"
	fileVar .= "timerSize:"
	fileVar .= overlayTextTimer.size
	fileVar .= "`n"
	fileVar .= "ccSize:"
	fileVar .= overlayTextccTimer.size
	fileVar .= "`n"
	fileVar .= "resisSize:"
	fileVar .= overlayResists.size
	fileVar .= "`n"
	fileVar .= "MX:"
	fileVar .= MX
	fileVar .= "`n"
	fileVar .= "MY:"
	fileVar .= MY
	fileVar .= "`n"
	fileVar .= "w:"
	fileVar .= w
	fileVar .= "`n"
	fileVar .= "h:"
	fileVar .= h
	fileVar .= "`n"
	fileVar .= "show:"
	fileVar .= showGuild
	fileVar .= showClass
	fileVar .= showLevel
	fileVar .= showRR
	fileVar .= showSoloKills
	fileVar .= showResists
	fileVar .= showEvent
	fileVar .= showTimer
	fileVar .= showccTimer
	fileVar .= "`n"
	fileVar .= "server:"
	fileVar .= serverName
	fileVar .= "`n"
	fileVar .= "edenHeraldCookie:"
	fileVar .= edenHeraldCookieRead
	fileVar .= "`n"
	fileVar .= "edenHeraldUserAgent:"
	fileVar .= edenHeraldUserAgent
	fileVar .= "`n"
	fileVar .= "font:"
	fileVar .= cfgFont
	fileVar .= "`n"
	FileAppend, %fileVar%, cfg.ini
	MsgBox, 262144, Herald Helper, Config saved!
	DeleteObject(fileVar)
}

updateEdenCookie()
{
	if(serverName == "eden" && initDone)
	{
		; 1. Define paths (adjust if needed)
		nodePath := "node.exe"  ; Or full path like "C:\Program Files\nodejs\node.exe"
		scriptPath := "edenHeaders.js"
		
		; 2. Run Node.js script and capture output
		cmd := ComSpec . " /c " . nodePath . " " . scriptPath
		exec := ComObjCreate("WScript.Shell").Exec(cmd)
		jsonOutput := exec.StdOut.ReadAll()
		jsonResult := JSON.Load(jsonOutput)
		
		if (jsonResult.success) {
			; Extract userAgent (always available)
			userAgent := jsonResult.data.userAgent
			
			; Initialize variables
			eden_daoc_sid := ""
			POWSESS := ""
			
			; Check if isLoggedIn is an ElementHandle
			if (jsonResult.data.isLoggedIn._type == "ElementHandle") {
				; Extract cookies
				for index, cookie in jsonResult.data.cookies {
					if (cookie.name == "eden_daoc_sid") {
						eden_daoc_sid := cookie.value
					}
					else if (cookie.name == "POWSESS") {
						POWSESS := cookie.value
					}
				}
				edenHeraldUserAgent := userAgent
				edenHeraldCookie := "eden_daoc_sid=" + eden_daoc_sid
				edenHeraldCookie .= ";POWSESS="
				edenHeraldCookie .= POWSESS
				edenHeraldCookie .= ";"
				;MsgBox, %edenHeraldCookie%
				
			}
		} else {
			jsonResultError := jsonResult.error
			MsgBox, Error: %jsonResultError%
		}
	}
}

setLocationOverlay()
{
	While !GetKeyState("LButton") 
	{ 
		MouseGetPos, xpos, ypos
		overlayBackground.y	:= ypos
		overlayBackground.x := xpos
		DeleteObject(xpos)
		DeleteObject(ypos)
		guiiInfoDraw()
	} 
	
}

moveResists()
{
	While !GetKeyState("LButton") 
	{
		MouseGetPos, xpos, ypos
		overlayResists.y := ypos
		overlayResists.x := xpos
		DeleteObject(xpos)
		DeleteObject(ypos)
		guiiInfoDraw()
	} 
	
}

setLocationTimerOverlay()
{
	While !GetKeyState("LButton") 
	{ 
		MouseGetPos, xpos, ypos
		overlayBackgroundTimer.y	:= ypos
		overlayBackgroundTimer.x := xpos
		DeleteObject(xpos)
		DeleteObject(ypos)
		addToTimerInformation(lastName, "s", 1, "m")
		addToTimerInformation(lastName, "m", 1, "s")
		addToTimerInformation(lastName, "s", 1, "r")
		guiiInfoDraw()
	}
}

setLocationccTimerOverlay()
{
	While !GetKeyState("LButton")
	{ 
		MouseGetPos, xpos, ypos
		overlayBackgroundccTimer.y := ypos
		overlayBackgroundccTimer.x := xpos
		DeleteObject(xpos)
		DeleteObject(ypos)
		addToTimerInformation(lastName, "s", 1, "m")
		addToTimerInformation(lastName, "m", 1, "s")
		addToTimerInformation(lastName, "s", 1, "r")
		guiiInfoDraw()
	} 
}

changeTextSize(direction)
{
	if(direction)
	{
		if(overlayText.size < 100 and overlayText.size >= 8)
		{
			overlayText.size := overlayText.size+1
			overlayTextTimer.size := overlayText.size+1
			overlayTextccTimer.size := overlayTextccTimer.size+1
		}
	} else
	{
		if(overlayText.size <= 100 and overlayText.size > 8)
		{
			overlayText.size := overlayText.size-1
			overlayTextTimer.size := overlayText.size-1
			overlayTextccTimer.size := overlayTextccTimer.size-1
		}
	}
	getHeraldInfo()
}

getHeraldInfo()
{
	IF (name == "Vorel")
		name := "Yorel"
	
	attempts := 1
	maxAttempts := 1
	realName := name
	checkName := false
	
	if (SubStr(name, 1, 1) = "V") {
		maxAttempts := 2
	}
	
	loop, %maxAttempts%
	{
		if (A_Index = 1) {
			name := name
		} else {
			name := "Y" . SubStr(name, 2)
		}
		switch serverName
		{
			case "phoenix":
			html := get("https://herald.playphoenix.online/c/"+name)
			UnHTML(html)y
			htmlLines := StrSplit(html, "`n")
			
			for index, value in htmlLines
			{
				rows.Insert(StrSplit(value, "`t"))
			}
			checkIfMainPage := rows[6][1]
			checkIfMainPage = %checkIfMainPage%
			IF (checkIfMainPage = "Characters - Phoenix Herald")
			{
				
				charName := rows[49][1]
				charName = %charName%
				
				charGuild := rows[51][1]
				charGuild = %charGuild%
				StringTrimRight, charGuild, charGuild, 3
				StringTrimLeft, charGuild, charGuild, 3
				
				charClass := rows[53][1]
				charClass = %charClass%
				
				charLevel := rows[57][1]
				charLevel = %charLevel%
				charLevel := RegExReplace(charLevel,"Level ")
				
				charRankLevel := rows[61][1]
				charRankLevel = %charRankLevel%
				charRankLevel := RegExReplace(charRankLevel,"ealm Rank ","")
				
				charRace := rows[65][1]
				charRace = %charRace%
				
				charRankName := rows[67][1]
				charRankName = %charRankName%
				
				soloKills := rows[271][1]
				soloKills = %soloKills%
				
				
				guiiInfoArray.cName := charName
				guiiInfoArray.cGuild := charGuild
				guiiInfoArray.cClass := charClass
				guiiInfoArray.cLevel := charLevel
				guiiInfoArray.cRankLevel := charRankLevel
				guiiInfoArray.cSolo := soloKills
			;writeCharacter()
			;DebugMessage(guiiInfo)
			;DebugMessage(charName)
			;DebugMessage(charClass)
			;DebugMessage(charLevel)
			;DebugMessage(charRankLevel)
			;DebugMessage(charRace)
			;DebugMessage(charRankName)
			;DebugMessage(charGuild)
			;DebugMessage(soloKills)
			;DebugMessage(guiiInfo)
				guiiInfoDraw()
			}
			
			case "titan":
			html := get("https://titan.api.opendaoc.com/player/"+name)
			jsonResult := JSON.Load(html)
			charName := jsonResult.name
			charRankName := 
			charGuild := jsonResult.guild
			charRace := jsonResult.race
			charClass := jsonResult.class
			charRankLevel := jsonResult.realmRank
			soloKills := jsonResult.killsAlbionSolo + jsonResult.killsMidgardSolo + jsonResult.killsHiberniaSolo
			charLevel := jsonResult.level
			
			guiiInfoArray.cName := charName
			guiiInfoArray.cGuild := charGuild
			guiiInfoArray.cClass := charClass
			guiiInfoArray.cLevel := charLevel
			guiiInfoArray.cRankLevel := charRankLevel
			guiiInfoArray.cSolo := soloKills
			guiiInfoDraw()
			
			case "celestius":
			ComObjError(false)
			WinHTTP := ComObjCreate("WinHTTP.WinHttpRequest.5.1")
			WinHTTP.Open("POST", "https://s695ojsti6.execute-api.eu-west-1.amazonaws.com/dev/graphql", true)
			WinHTTP.SetRequestHeader("Content-Type", "application/json")
			x := """" . name . """"
			body = {"query":"query CharacterScreenQuery(\r\n $name: String!\r\n) {\r\n character(name: $name) {\r\n name\r\n realm {\r\n name\r\n }\r\n class {\r\n name\r\n }\r\n guild {\r\n name\r\n }\r\n realmRank\r\n realmPoints {\r\n total\r\n }\r\n rvrStats {\r\n deaths\r\n kills\r\n soloKills\r\n deathBlows\r\n realms {\r\n realm {\r\n name\r\n }\r\n kills\r\n soloKills\r\n deathBlows\r\n }\r\n }\r\n }\r\n}","variables": {"name": %x%}}
			WinHTTP.Send(body)
			WinHTTP.WaitForResponse()
			body =
			result := WinHTTP.ResponseText
			jsonResult := JSON.Load(result)
			
			charName := jsonResult.data.character.name
			charRankName := 
			charGuild := jsonResult.data.character.guild.name
			charRace := 
			charClass := jsonResult.data.character.class.name
			charRankLevel := jsonResult.data.character.realmrank
			soloKills := jsonResult.data.character.rvrStats.soloKills
			charLevel := 
			
			guiiInfoArray.cName := charName
			guiiInfoArray.cGuild := charGuild
			guiiInfoArray.cClass := charClass
			guiiInfoArray.cLevel := charLevel
			guiiInfoArray.cRankLevel := charRankLevel
			guiiInfoArray.cSolo := soloKills
		;writeCharacter()
		;DebugMessage(guiiInfo)
		;DebugMessage(charName)
		;DebugMessage(charClass)
		;DebugMessage(charLevel)
		;DebugMessage(charRankLevel)
		;DebugMessage(charRace)
		;DebugMessage(charRankName)
		;DebugMessage(charGuild)
		;DebugMessage(soloKills)
		;DebugMessage(guiiInfo)
			guiiInfoDraw()
			
			case "eden":
		;edenHeraldCookie := "eden_daoc_sid=a98f1bc630432c2ced5ccecc70398abd"
		;DebugMessage(edenHeraldUserAgent)
			if(edenHeraldCookie AND edenHeraldUserAgent)
			{
				whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
				whr.Open("GET", "https://eden-daoc.net/hrald/proxy.php?player/"+name, true)
				whr.SetRequestHeader("cookie", edenHeraldCookie)
				whr.SetRequestHeader("user-agent", edenHeraldUserAgent)
				whr.SetRequestHeader("x-herald-api", "minified")		
				whr.Send()
		; Using 'true' above and the call below allows the script to remain responsive.w
				whr.WaitForResponse()
				result := whr.ResponseText
				
				;Custom Change with js Script to fetch cookies
				if(whr.status == 401)
				{
					updateEdenCookie()
					whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
					whr.Open("GET", "https://eden-daoc.net/hrald/proxy.php?player/"+name, true)
					whr.SetRequestHeader("cookie", edenHeraldCookie)
					whr.SetRequestHeader("user-agent", edenHeraldUserAgent)
					whr.SetRequestHeader("x-herald-api", "minified")	
					whr.Send()
					whr.WaitForResponse()
					result := whr.ResponseText
					
				}
				jsonResult := JSON.Load(result)
				
				charName := jsonResult.name
				charRankName := 
				charGuild := jsonResult.guild_name
				charRace := edenRaceList[jsonResult.race_id]
				charClass := edenClassList[jsonResult.class]
				charRankLevel := jsonResult.realmRank
				
				convertEXPAndRP(jsonResult.experience, 0)
				convertEXPAndRP(jsonResult.realm_points, 1)
				
				guiiInfoArray.cGuild := charGuild
				guiiInfoArray.cClass := charClass
				guiiInfoArray.cLevel := charLevel
				guiiInfoArray.cRankLevel := charRankLevel
				guiiInfoArray.cName := charName
				
				guiiInfoDraw()
				
				whr.Open("GET", "https://eden-daoc.net/hrald/proxy.php?rank/pvp/"+name, true)
				whr.SetRequestHeader("cookie", edenHeraldCookie)
				whr.SetRequestHeader("user-agent", edenHeraldUserAgent)
				whr.SetRequestHeader("x-herald-api", "minified")	
				whr.Send()
				whr.WaitForResponse()
				result2 := whr.ResponseText
				jsonResult2 := JSON.Load(result2)
				
				;loopBoolean := false
				;while(loopBoolean)
				;{
				;	html := get("https://eden-daoc.net/hrald/proxy.php?player/"+name)
				;	IF InStr(html, """show_in_herald"":") AND !InStr(html, """show_in_herald"":&nbsp")
				;		loopBoolean := false
				;	ELSE IF !InStr(html, """show_in_herald"":") AND !InStr(html, """show_in_herald"":&nbsp")
				;		break
				;}
				;loopBoolean2 := false
				
				;while(loopBoolean2 AND showSoloKills)
				;{
				;	html2 := get("https://eden-daoc.net/hrald/proxy.php?rank/pvp/"+name)
				;	IF InStr(html2, """show_in_herald"":") AND !InStr(html2, """show_in_herald"":&nbsp")
				;		loopBoolean2 := false
				;	ELSE IF !InStr(html2, """show_in_herald"":") AND !InStr(html2, """show_in_herald"":&nbsp")
				;		break
				;}
				
				StringTrimLeft, html, html, 74
				
				StringTrimLeft, html2, html2, 74
		;jsonResult := JSON.Load(html)
		;jsonResult2 := JSON.Load(html2)
				
				
				if(!showSoloKills)
					soloKills := 
				else
					soloKills := jsonResult2.solo_kills
				
				
				
		;charLevel := jsonResult.level
				
				
				
				
				guiiInfoArray.cSolo := soloKills
				guiiInfoDraw()
			}
			
			case "blackthorn":
			
			whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
			whr.Open("GET", "https://herald.blackthorn-daoc.com/stats/player/"+name, true)	
			whr.Send()
			; Using 'true' above and the call below allows the script to remain responsive.w
			whr.WaitForResponse()
			result := whr.ResponseText
			
			
			
			; Name: Matches ""Name Lcell player"" and captures the name (.*?)
			RegExMatch(result, "class=""Name Lcell player"">(.*?)</td>", MatchName)
			
			; LVL: Matches ""Level Lcell level centerT"" and captures the LVL (.*?)
			RegExMatch(result, "class=""Level Lcell level centerT"">(.*?)</td>", MatchLVL)
			
			; RR: Matches ""RR Lcell level centerT"" and captures the RR (.*?)
			RegExMatch(result, "class=""RR Lcell level centerT"">(.*?)</td>", MatchRR)
			
			; Realm: Matches ""realmName centerT"" and captures the Realm (.*?)
			RegExMatch(result, "class=""realmName centerT"">(.*?)</td>", MatchRealm)
			
			; Race: Matches ""Race centerT"" and captures the Race (.*?)
			RegExMatch(result, "<td class=""Race centerT"">(.*?)</td>", MatchRace)
			
			; Class: Matches ""className centerT"" and captures the Class (.*?)
			RegExMatch(result, "<td class=""className centerT"">(.*?)</td>", MatchClass)
			
			
			; Guild: Finds the guild <a> tag and captures the text (.*?)
			RegExMatch(result, "href=""/stats/guild/(.*?)"">(.*?)</a>", MatchGuild)
			
			; Solo: Finds the <td>Solo</td> header and captures the content of the next <td>.
			RegExMatch(result, "<td>Solo</td>\n                          <td>(.*?)</td>", MatchSolo)
			
			charName := MatchName1
			charRankName := 
			charGuild := MatchGuild1
			charRace := MatchRace1
			charClass := MatchClass1
			charRankLevel := MatchRR1
			
			
			guiiInfoArray.cGuild := MatchGuild1
			guiiInfoArray.cClass := charClass
			guiiInfoArray.cLevel := MatchLVL1
			guiiInfoArray.cRankLevel := charRankLevel
			guiiInfoArray.cName := charName
			
			if(!showSoloKills)
				soloKills := 0
			else
				soloKills := MatchSolo1
			
			guiiInfoArray.cSolo := MatchSolo1
			guiiInfoDraw()
		}
		if(guiiInfoArray.cName == "")
		{
			guiiInfoArray.cName := name
		}
		else
		{
			checkName := true
		}
		name := realName
		;guiiInfoDraw()
		if(checkName) {
			break
		}
	}
	
	rows := []
	document := 
	htlm := 
	htmlLines := 
	checkIfMainPage := 
	charName := 
	charGuild := 
	charClass := 
	charLevel := 
	charRankLevel := 
	charRankName := 
	soloKills := 
	r := 
	return
}

convertEXPAndRP(playerValue, EXPorRP)
{
	switch EXPorRP
	{
		case 0:
		for i, valueArray in edenXPList
		{
			
			if(playerValue < valueArray)
				break
			charLevel := i
		}
		/*
			for i, valueArray in edenXPList
			{
				charLevel := valueArray[1]
				if(playerValue < valueArray[2])
					break
			}
		*/
		
		case 1:
		for i, valueArray in edenRPList
		{
			
			if(playerValue < valueArray)
				break
			charRankLevel := "RR"
			charRankLevel .= Floor((i)/10)+1
			charRankLevel .= "L"
			charRankLevel .= Mod(i, 10)
		}
	}
}

guiiccInfoDraw()
{
	if(showccTimer)
	{
		
		if(guiiInfoMezzTime > 0)
		{
			guiicc := guiiInfoMezzTime
			overlayTextccTimer.color := "dcd335"
			guiiccTimer.draw(guiicc, overlayBackgroundccTimer, overlayTextccTimer)
		}
		if(guiiInfoStunTime > 0)
		{
			guiicc := guiiNewLine
			guiicc .= guiiInfoStunTime
			overlayTextccTimer.color := "cf33a4"
			guiiccTimer.draw(guiicc, overlayBackgroundccTimer, overlayTextccTimer)
		}
		if(guiiInfoRootTime > 0)
		{
			guiicc := guiiNewLine
			guiicc .= guiiNewLine
			guiicc .=  guiiInfoRootTime
			overlayTextccTimer.color := "a87130"
			guiiccTimer.draw(guiicc, overlayBackgroundccTimer, overlayTextccTimer)
		}
	}
	guiiccTimer.Render()
}



guiiResistsDraw()
{
	if(showResists)
	{
		colorOrder := Array(WHITE, WHITE, WHITE)
		
		InStr(det, guiiInfoArray.cClass)
		if (InStr(PLATELEATHER, guiiInfoArray.cClass)) {
			colorOrder := Array(RED, WHITE, GREEN)
		}
		else if (InStr(SCALE, guiiInfoArray.cClass)) {
			colorOrder := Array(WHITE, GREEN, RED)
		}
		else if (InStr(CHAINSTUDDED, guiiInfoArray.cClass)) {
			colorOrder := Array(GREEN, WHITE, RED)
		}
		else if (InStr(CHAINMID, guiiInfoArray.cClass)) {
			colorOrder := Array(GREEN, RED, WHITE)
		}
		else if (InStr(REINFORCEDLEAHTER, guiiInfoArray.cClass)) {
			colorOrder := Array(WHITE, RED, GREEN)
		}
		else if (InStr(STUDDEDLEATHER, guiiInfoArray.cClass)) {
			colorOrder := Array(RED, GREEN, WHITE)
		}
		else {
			colorOrder := Array(WHITE, WHITE, WHITE)
		}
		resists := thrust
		overlayResists.color := colorOrder.pop()
		guiiResists.draw(resists, overlayBackgroundResists, overlayResists)
		
		resists := guiiNewLine
		resists .= slash
		overlayResists.color := colorOrder.pop()
		guiiResists.draw(resists, overlayBackgroundResists, overlayResists)
		
		resists := guiiNewLine
		resists .= guiiNewLine
		resists .=  crush
		overlayResists.color := colorOrder.pop()
		guiiResists.draw(resists, overlayBackgroundResists, overlayResists)
	}
	guiiResists.Render()
}

guiiInfoDraw()
{
	if (InStr(classesAlb, charClass) AND InStr(racesAlb, charRace))
	{
		overlayText.color := "C82E2E"
	} else if (InStr(classesMid, charClass) AND InStr(racesMid, charRace))
	{
		overlayText.color := "0275D8"
	} else if (InStr(classesHib, charClass) AND InStr(racesHib, charRace))
	{
		overlayText.color := "5CB85C"
	}
	guiiInfo := guiiInfoArray.cName
	if(showGuild)
	{
		guiiInfo .= guiiSpace
		guiiInfo .= guiiInfoArray.cGuild
	}
	guiiInfo .= guiiNewLine
	if(showClass)
	{
		guiiInfo .= guiiInfoArray.cClass
		guiiInfo .= guiiSpace
	}
	if(showLevel)
	{
		guiiInfo .= guiiInfoArray.cLevel
		guiiInfo .= guiiSpace
	}
	if(showRR)
	{
		guiiInfo .= guiiInfoArray.cRankLeveL
	}
	if(showSoloKills)
	{
		guiiInfo .= guiiNewLine
		guiiInfo .= guiiInfoArray.cSolo
	}
	guii.draw(guiiInfo, overlayBackground, overlayText)
	drawTimerInformation()
	guiiccInfoDraw()
	guiiResistsDraw()
	guii.Render()
}

get(URL) {
	ComObjError(false)
	r := ComObjCreate("WinHttp.WinHttpRequest.5.1")
	r.Open("GET", URL, true),r.Send(),r.WaitForResponse()
	DeleteObject(URL)
	return r.ResponseText
}


UnHTML(PPT:=1, RUE:=1 ) {                              ; By SKAN on D1BN/D33P @ tiny.cc/unhtml
	Local Asc, E, K, P:=1
	Static HEN := "
 (  LTrim Join
             {HTML4:,Aacute:193,aacute:225,Acirc:194,acirc:226,acute:180,add:43,AElig:198,aelig:230,
    Agrave:192,agrave:224,alefsym:8501,Alpha:913,alpha:945,amp:38,and:8743,ang:8736,apos:39,Aring:19
    7,aring:229,asymp:8776,Atilde:195,atilde:227,Auml:196,auml:228,bdquo:8222,Beta:914,beta:946,brvb
    ar:166,bull:8226,cap:8745,Ccedil:199,ccedil:231,cedil:184,cent:162,Chi:935,chi:967,circ:710,club
    s:9827,cong:8773,copy:169,crarr:8629,cup:8746,curren:164,dagger:8224,Dagger:8225,darr:8595,dArr:
    8659,deg:176,Delta:916,delta:948,diams:9830,divide:247,Eacute:201,eacute:233,Ecirc:202,ecirc:234
    ,Egrave:200,egrave:232,empty:8709,emsp:8195,ensp:8194,Epsilon:917,epsilon:949,equal:61,equiv:880
    1,Eta:919,eta:951,ETH:208,eth:240,Euml:203,euml:235,euro:8364,exclamation:33,exist:8707,fnof:402
    ,forall:8704,frac12:189,frac14:188,frac34:190,frasl:8260,Gamma:915,gamma:947,ge:8805,gt:62,harr:
    8596,hArr:8660,hearts:9829,hellip:8230,horbar:8213,Iacute:205,iacute:237,Icirc:206,icirc:238,iex
    cl:161,Igrave:204,igrave:236,image:8465,infin:8734,int:8747,Iota:921,iota:953,iquest:191,isin:87
    12,Iuml:207,iuml:239,Kappa:922,kappa:954,Lambda:923,lambda:955,lang:9001,laquo:171,larr:8592,lAr
    r:8656,lceil:8968,ldquo:8220,le:8804,lfloor:8970,lowast:8727,loz:9674,lrm:8206,lsaquo:8249,lsquo
    :8216,lt:60,macr:175,mdash:8212,micro:181,middot:183,minus:8722,Mu:924,mu:956,nabla:8711,nbsp:16
    0,ndash:8211,ne:8800,ni:8715,not:172,notin:8713,nsub:8836,Ntilde:209,ntilde:241,Nu:925,nu:957,Oa
    cute:211,oacute:243,Ocirc:212,ocirc:244,OElig:338,oelig:339,Ograve:210,ograve:242,oline:8254,Ome
    ga:937,omega:969,Omicron:927,omicron:959,oplus:8853,or:8744,ordf:170,ordm:186,Oslash:216,oslash:
    248,Otilde:213,otilde:245,otimes:8855,Ouml:214,ouml:246,para:182,part:8706,percent:37,permil:824
    0,perp:8869,Phi:934,phi:966,Pi:928,pi:960,piv:982,plusmn:177,pound:163,prime:8242,Prime:8243,pro
    d:8719,prop:8733,Psi:936,psi:968,quot:34,radic:8730,rang:9002,raquo:187,rarr:8594,rArr:8658,rcei
    l:8969,rdquo:8221,real:8476,reg:174,rfloor:8971,Rho:929,rho:961,rlm:8207,rsaquo:8250,rsquo:8217,
    sbquo:8218,Scaron:352,scaron:353,sdot:8901,sect:167,shy:173,Sigma:931,sigma:963,sigmaf:962,sim:8
    764,spades:9824,sub:8834,sube:8838,sum:8721,sup:8835,sup1:185,sup2:178,sup3:179,supe:8839,szlig:
    223,Tau:932,tau:964,there4:8756,Theta:920,theta:952,thetasym:977,thinsp:8201,THORN:222,thorn:254
    ,tilde:732,times:215,trade:8482,Uacute:218,uacute:250,uarr:8593,uArr:8657,Ucirc:219,ucirc:251,Ug
    rave:217,ugrave:249,uml:168,upsih:978,Upsilon:933,upsilon:965,Uuml:220,uuml:252,weierp:8472,Xi:9
    26,xi:958,Yacute:221,yacute:253,yen:165,yuml:255,Yuml:376,Zeta:918,zeta:950,zwj:8205,zwnj:8204,}
 )"
	
	html := !PPT ? html : RegExReplace(html,"<[^>]+>") ; Remove all text wrapped within "<" and ">"
	
	While ( P := RegExMatch(html, "(?<!&)&[#a-zA-Z0-9]+;", E, P) )  and  (K := Trim(E,"&#;") )
	{
		Asc  := ( SubStr(E,1,3) = "&#x" ? ("0" . K) 
            :    SubStr(E,1,2) = "&#"  ? (      K)  
            :    RegExMatch(HEN,   "(?<=," . K . ":)[0-9]+(?=,)", Asc) ? Asc
            :    RegExMatch(HEN, "i)(?<=," . K . ":)[0-9]+(?=,)", Asc) ? Asc : 0) 
		
     , html  := RegExReplace(html, E, Asc ? Chr(Asc) : RUE=0 ? ("&" . E)  : "")
	}
	DeleteObject(HEN)
	DeleteObject(Asc)
	DeleteObject(E)
	DeleteObject(K)
	DeleteObject(P)
	DeleteObject(PPT)
	DeleteObject(RUE)
}

HBitmapToRandomAccessStream(hBitmap) {
	DllCall("Ole32\CreateStreamOnHGlobal", "Ptr", 0, "UInt", true, "PtrP", pIStream, "UInt")
	
	VarSetCapacity(PICTDESC, 8 + A_PtrSize*2, 0)
	NumPut(8, PICTDESC)
	NumPut(PICTYPE_BITMAP, PICTDESC, 4)
	NumPut(hBitmap, PICTDESC, 8)
	riid := CLSIDFromString(IID_IPicture, GUID1)
	DllCall("OleAut32\OleCreatePictureIndirect", "Ptr", &PICTDESC, "Ptr", riid, "UInt", false, "PtrP", pIPicture, "UInt")
   ; IPicture::SaveAsFile
	DllCall(NumGet(NumGet(pIPicture+0) + A_PtrSize*15), "Ptr", pIPicture, "Ptr", pIStream, "UInt", true, "UIntP", size, "UInt")
	riid := CLSIDFromString(IID_IRandomAccessStream, GUID2)
	DllCall("ShCore\CreateRandomAccessStreamOverStream", "Ptr", pIStream, "UInt", BSOS_DEFAULT, "Ptr", riid, "PtrP", pIRandomAccessStream, "UInt")
	ObjRelease(IID_IRandomAccessStream)
	ObjRelease(pIPicture)
	ObjRelease(pIStream)
	Return pIRandomAccessStream
}

CLSIDFromString(IID, ByRef CLSID) {
	VarSetCapacity(CLSID, 16, 0)
	if res := DllCall("ole32\CLSIDFromString", "WStr", IID, "Ptr", &CLSID, "UInt")
		throw Exception("CLSIDFromString failed. Error: " . Format("{:#x}", res))
	Return &CLSID
}

SCW_SelectAreaMod(Options="") {
	CoordMode, Mouse, Screen
	MouseGetPos, MX, MY
	loop, parse, Options, %A_Space%
	{
		Field := A_LoopField
		FirstChar := SubStr(Field,1,1)
		if FirstChar contains c,t,g,m
		{
			StringTrimLeft, Field, Field, 1
			%FirstChar% := Field
		}
	}
	c := (c = "") ? "Red" : c, t := (t = "") ? "100" : t, g := (g = "") ? "99" : g
	Gui %g%: Destroy
	Gui %g%: +AlwaysOnTop -caption +Border +ToolWindow +LastFound -DPIScale ;provided from rommmcek 10/23/16
	
	WinSet, Transparent, %t%
	Gui %g%: Color, %c%
	Hotkey := RegExReplace(A_ThisHotkey,"^(\w* & |\W*)")
	While, (GetKeyState(Hotkey, "p"))
	{
		Sleep, 10
		MouseGetPos, MXend, MYend
		w := abs(MX - MXend), h := abs(MY - MYend)
		X := (MX < MXend) ? MX : MXend
		Y := (MY < MYend) ? MY : MYend
		Gui %g%: Show, x%X% y%Y% w%w% h%h% NA
	}
	Gui %g%: Destroy
	MouseGetPos, MXend, MYend
	If ( MX > MXend )
		temp := MX, MX := MXend, MXend := temp
	If ( MY > MYend )
		temp := MY, MY := MYend, MYend := temp
	Return MX "|" MY "|" w "|" h
}

CreateClass(string, interface, ByRef Class){
	CreateHString(string, hString)
	VarSetCapacity(GUID, 16)
	DllCall("ole32\CLSIDFromString", "wstr", interface, "ptr", &GUID)
	result := DllCall("Combase.dll\RoGetActivationFactory", "ptr", hString, "ptr", &GUID, "ptr*", Class)
	if (result != 0){
		if (result = 0x80004002)
			msgbox No such interface supported
		else if (result = 0x80040154)
			msgbox Class not registered
		else
			msgbox error: %result%
		ExitApp
	}
	DeleteHString(hString)
}

CreateHString(string, ByRef hString){
	DllCall("Combase.dll\WindowsCreateString", "wstr", string, "uint", StrLen(string), "ptr*", hString)
}

DeleteHString(hString){
	DllCall("Combase.dll\WindowsDeleteString", "ptr", hString)
}



WaitForAsync(Object, ByRef ObjectResult){
	AsyncInfo := ComObjQuery(Object, IAsyncInfo := "{00000036-0000-0000-C000-000000000046}")
	loop	{
		DllCall(NumGet(NumGet(AsyncInfo+0)+7*A_PtrSize), "ptr", AsyncInfo, "uint*", status)   ; IAsyncInfo.Status
		if (status != 0)		{
			ObjRelease(AsyncInfo)
			AsyncInfo := ""
			break
		}
		sleep 10
	}
	
	DllCall(NumGet(NumGet(Object+0)+8*A_PtrSize), "ptr", Object, "ptr*", ObjectResult)   ; GetResults
}

OCR(IRandomAccessStream){
	static OcrEngineClass, OcrEngineObject, MaxDimension, Language, LanguageFactory, LanguageClass, LanguageObject, CurrentLanguage, StorageFileClass, BitmapDecoderClass, GlobalizationPreferencesStatics
	if (OcrEngineClass = "")	{
		CreateClass("Windows.Globalization.Language", ILanguageFactory := "{9B0252AC-0C27-44F8-B792-9793FB66C63E}", LanguageClass)
		CreateClass("Windows.Graphics.Imaging.BitmapDecoder", IStorageFileStatics := "{438CCB26-BCEF-4E95-BAD6-23A822E58D01}", BitmapDecoderClass)
		CreateClass("Windows.Media.Ocr.OcrEngine", IOcrEngineStatics := "{5BFFA85A-3384-3540-9940-699120D428A8}", OcrEngineClass)
		DllCall(NumGet(NumGet(OcrEngineClass+0)+6*A_PtrSize), "ptr", OcrEngineClass, "uint*", MaxDimension)   ; MaxImageDimension
	}
	if(languageText == "" and CurrentLanguage == "")
	{
		if (GlobalizationPreferencesStatics = "")
			CreateClass("Windows.System.UserProfile.GlobalizationPreferences", IGlobalizationPreferencesStatics := "{01BF4326-ED37-4E96-B0E9-C1340D1EA158}", GlobalizationPreferencesStatics)
		DllCall(NumGet(NumGet(GlobalizationPreferencesStatics+0)+9*A_PtrSize), "ptr", GlobalizationPreferencesStatics, "ptr*", LanguageList)   ; get_Languages
		DllCall(NumGet(NumGet(LanguageList+0)+7*A_PtrSize), "ptr", LanguageList, "int*", count)   ; count
		loop % count
		{
			DllCall(NumGet(NumGet(LanguageList+0)+6*A_PtrSize), "ptr", LanguageList, "int", A_Index-1, "ptr*", hString)   ; get_Item
			DllCall(NumGet(NumGet(LanguageClass+0)+6*A_PtrSize), "ptr", LanguageClass, "ptr", hString, "ptr*", LanguageTest)   ; CreateLanguage
			DllCall(NumGet(NumGet(OcrEngineClass+0)+8*A_PtrSize), "ptr", OcrEngineClass, "ptr", LanguageTest, "int*", bool)   ; IsLanguageSupported
			if (bool = 1)
			{
				DllCall(NumGet(NumGet(LanguageTest+0)+6*A_PtrSize), "ptr", LanguageTest, "ptr*", hText)
				buffer := DllCall("Combase.dll\WindowsGetStringRawBuffer", "ptr", hText, "uint*", length, "ptr")
				lText := StrGet(buffer, "UTF-16") "`n"
				languageText .= (lText)lText
			}
			ObjRelease(LanguageTest)
		}
		ObjRelease(LanguageList)
	}
	if (CurrentLanguage == ""){
		if (LanguageObject != ""){
			ObjRelease(LanguageObject)
			ObjRelease(OcrEngineObject)
			LanguageObject := OcrEngineObject := ""
		}
		if(InStr(languageText,"en-sss"))
		{
			lang := "en"
		}
		else
		{
			RegExMatch(languageText, "(.*)`n", s)
			loop, parse, languageText, `n
			{
				lang := a_loopfield
				break
			}
		}
		CreateHString(lang, hString)
		DllCall(NumGet(NumGet(LanguageClass+0)+6*A_PtrSize), "ptr", LanguageClass, "ptr", hString, "ptr*", LanguageObject)   ; CreateLanguage
		DeleteHString(hString)
		DllCall(NumGet(NumGet(OcrEngineClass+0)+9*A_PtrSize), "ptr", OcrEngineClass, ptr, LanguageObject, "ptr*", OcrEngineObject)   ; TryCreateFromLanguage
		if (OcrEngineObject = 0){
			msgbox Can not use language "%language%" for OCR, please install language pack.
			ExitApp
		}
		CurrentLanguage := lang
	}
	
	DllCall(NumGet(NumGet(BitmapDecoderClass+0)+14*A_PtrSize), "ptr", BitmapDecoderClass, "ptr", IRandomAccessStream, "ptr*", BitmapDecoderObject)   ; CreateAsync
	WaitForAsync(BitmapDecoderObject, BitmapDecoderObject1)
	BitmapFrame := ComObjQuery(BitmapDecoderObject1, IBitmapFrame := "{72A49A1C-8081-438D-91BC-94ECFC8185C6}")
	DllCall(NumGet(NumGet(BitmapFrame+0)+12*A_PtrSize), "ptr", BitmapFrame, "uint*", width)   ; get_PixelWidth
	DllCall(NumGet(NumGet(BitmapFrame+0)+13*A_PtrSize), "ptr", BitmapFrame, "uint*", height)   ; get_PixelHeight
	if (width > MaxDimension) or (height > MaxDimension){
		msgbox Image is to big - %width%x%height%.`nIt should be maximum - %MaxDimension% pixels
		ExitApp
	}
	SoftwareBitmap := ComObjQuery(BitmapDecoderObject1, IBitmapFrameWithSoftwareBitmap := "{FE287C9A-420C-4963-87AD-691436E08383}")
	DllCall(NumGet(NumGet(SoftwareBitmap+0)+6*A_PtrSize), "ptr", SoftwareBitmap, "ptr*", BitmapFrame1)   ; GetSoftwareBitmapAsync
	
	WaitForAsync(BitmapFrame1, BitmapFrame2)
	DllCall(NumGet(NumGet(OcrEngineObject+0)+6*A_PtrSize), "ptr", OcrEngineObject, ptr, BitmapFrame2, "ptr*", OcrResult)   ; RecognizeAsync
	
	WaitForAsync(OcrResult, OcrResult1)
	DllCall(NumGet(NumGet(OcrResult1+0)+6*A_PtrSize), "ptr", OcrResult1, "ptr*", lines)   ; get_Lines
	DllCall(NumGet(NumGet(lines+0)+7*A_PtrSize), "ptr", lines, "int*", count)   ; count
	loop % count {
		DllCall(NumGet(NumGet(lines+0)+6*A_PtrSize), "ptr", lines, "int", A_Index-1, "ptr*", OcrLine)
		DllCall(NumGet(NumGet(OcrLine+0)+7*A_PtrSize), "ptr", OcrLine, "ptr*", hText) 
		buffer := DllCall("Combase.dll\WindowsGetStringRawBuffer", "ptr", hText, "uint*", length, "ptr")
		text .= StrGet(buffer, "UTF-16") "`n"
		ObjRelease(OcrLine)
		OcrLine := ""
	}
	ObjRelease(BitmapDecoderObject)
	ObjRelease(BitmapDecoderObject1)
	ObjRelease(SoftwareBitmap)
	ObjRelease(BitmapFrame)
	ObjRelease(BitmapFrame1)
	ObjRelease(BitmapFrame2)
	ObjRelease(OcrResult)
	ObjRelease(OcrResult1)
	ObjRelease(lines)
	BitmapDecoderObject := BitmapDecoderObject1 := SoftwareBitmap := BitmapFrame := BitmapFrame1 := BitmapFrame2 := OcrResult := OcrResult1 := lines := ""
	return text
}

; Script:    Subtitle.ahk
{
	
; Author:    iseahound
; Version:   2018-04-17 (April 2018)
; Recent:    2018-05-15
	
;#Include %A_ScriptDir%\lib\Gdip_All.ahk
	
	
	class Subtitle {
		
		layers := {}, ScreenWidth := A_ScreenWidth, ScreenHeight := A_ScreenHeight
		
		__New(title := "") {
			global pToken
			if !(this.outer.Startup())
				if !(pToken)
					if !(this.pToken := Gdip_Startup())
						throw Exception("Gdiplus failed to start. Please ensure you have gdiplus on your system.")
			
			Gui, New, +LastFound +AlwaysOnTop -Caption -DPIScale +E0x80000 +ToolWindow +hwndhwnd
			this.hwnd := hwnd
			this.title := (title != "") ? title : "Subtitle_" this.hwnd
			DllCall("ShowWindow", "ptr",this.hwnd, "int",8)
			DllCall("SetWindowText", "ptr",this.hwnd, "str",this.title)
			this.hbm := CreateDIBSection(this.ScreenWidth, this.ScreenHeight)
			this.hdc := CreateCompatibleDC()
			this.obm := SelectObject(this.hdc, this.hbm)
			this.G := Gdip_GraphicsFromHDC(this.hdc)
			return this
		}
		
		__Delete() {
			global pToken
			if (this.outer.pToken)
				return this.outer.Shutdown()
			if (pToken)
				return
			if (this.pToken)
				return Gdip_Shutdown(this.pToken)
		}
		
		FreeMemory() {
			SelectObject(this.hdc, this.obm)
			DeleteObject(this.hbm)
			DeleteDC(this.hdc)
			Gdip_DeleteGraphics(this.G)
			return this
		}
		
		Destroy() {
			this.FreeMemory()
			DllCall("DestroyWindow", "ptr",this.hwnd)
			return this
		}
		
		Hide() {
			DllCall("ShowWindow", "ptr",this.hwnd, "int",0)
			return this
		}
		
		Show(i := 8) {
			DllCall("ShowWindow", "ptr",this.hwnd, "int",i)
			return this
		}
		
		ToggleVisible() {
			return (this.isVisible()) ? this.Hide() : this.Show()
		}
		
		isVisible() {
			return DllCall("IsWindowVisible", "ptr",this.hwnd)
		}
		
		AlwaysOnTop() {
			WinSet, AlwaysOnTop, Toggle, % "ahk_id" this.hwnd
			return this
		}
		
		Bottom() {
			WinSet, Bottom,, % "ahk_id" this.hwnd
			return this
		}
		
		ClickThrough() {
			_dhw := A_DetectHiddenWindows
			DetectHiddenWindows On
			WinGet, ExStyle, ExStyle, % "ahk_id" this.hwnd
			if (ExStyle & 0x20)
				WinSet, ExStyle, -0x20, % "ahk_id" this.hwnd
			else
				WinSet, ExStyle, +0x20, % "ahk_id" this.hwnd
			DetectHiddenWindows %_dhw%
			return this
		}
		
		Desktop() {
      ; Based on: https://www.codeproject.com/Articles/856020/Draw-Behind-Desktop-Icons-in-Windows-plus?msg=5478543#xx5478543xx
			DllCall("SendMessage", "ptr",WinExist("ahk_class Progman"), "uint",0x052C, "ptr",0x0000000D, "ptr",0)
			DllCall("SendMessage", "ptr",WinExist("ahk_class Progman"), "uint",0x052C, "ptr",0x0000000D, "ptr",1) ; Post-Creator's Update Windows 10.
			WinGet, windows, List, ahk_class WorkerW
			Loop, %windows%
				if (DllCall("FindWindowEx", "ptr",windows%A_Index%, "ptr",0, "str","SHELLDLL_DefView", "ptr",0) != 0)
					WorkerW := DllCall("FindWindowEx", "ptr",0, "ptr",windows%A_Index%, "str","WorkerW", "ptr",0)
			
			if (WorkerW) {
				this.Destroy()
				this.hwnd := WorkerW
				DllCall("SetWindowPos", "uint",WorkerW, "uint",1, "int",0, "int",0, "int",this.ScreenWidth, "int",this.ScreenHeight, "uint",0)
				this.base.FreeMemory := ObjBindMethod(this, "DesktopFreeMemory")
				this.base.Destroy := ObjBindMethod(this, "DesktopDestroy")
				this.hdc := DllCall("GetDCEx", "ptr",WorkerW, "ptr",0, "int",0x403)
				this.G := Gdip_GraphicsFromHDC(this.hdc)
			}
			return this
		}
		
		DesktopFreeMemory() {
			ReleaseDC(this.hdc)
			Gdip_DeleteGraphics(this.G)
			return this
		}
		
		DesktopDestroy() {
			this.FreeMemory()
			DllCall("SendMessage", "ptr",WinExist("ahk_class Progman"), "uint",0x052C, "ptr",0x0000000D, "ptr",0)
			DllCall("SendMessage", "ptr",WinExist("ahk_class Progman"), "uint",0x052C, "ptr",0x0000000D, "ptr",1)
			return this
		}
		
		Normal() {
			WinSet, AlwaysOnTop, Off, % "ahk_id" this.hwnd
			return this
		}
		
		DetectScreenResolutionChange(width := "", height := "") {
			width := (width) ? width : A_ScreenWidth
			height := (height) ? height : A_ScreenHeight
			if (width != this.ScreenWidth || height != this.ScreenHeight) {
				this.ScreenWidth := width, this.ScreenHeight := height
				SelectObject(this.hdc, this.obm)
				DeleteObject(this.hbm)
				DeleteDC(this.hdc)
				Gdip_DeleteGraphics(this.G)
				this.hbm := CreateDIBSection(this.ScreenWidth, this.ScreenHeight)
				this.hdc := CreateCompatibleDC()
				this.obm := SelectObject(this.hdc, this.hbm)
				this.G := Gdip_GraphicsFromHDC(this.hdc)
				loop % this.layers.maxIndex()
					this.Draw(this.layers[A_Index].1, this.layers[A_Index].2, this.layers[A_Index].3, pGraphics)
			}
		}
		
		Bitmap(x := "", y := "", w := "", h := "") {
			x := (x != "") ? x : this.x
			y := (y != "") ? y : this.y
			w := (w != "") ? w : this.xx - this.x
			h := (h != "") ? h : this.yy - this.y
			
			pBitmap := Gdip_CreateBitmap(this.ScreenWidth, this.ScreenHeight)
			pGraphics := Gdip_GraphicsFromImage(pBitmap)
			loop % this.layers.maxIndex()
				this.Draw(this.layers[A_Index].1, this.layers[A_Index].2, this.layers[A_Index].3, pGraphics)
			Gdip_DeleteGraphics(pGraphics)
			pBitmapCopy := Gdip_CloneBitmapArea(pBitmap, x, y, w, h)
			Gdip_DisposeImage(pBitmap)
			return pBitmapCopy ; Please dispose of this image responsibly.
		}
		
		hBitmap(alpha := 0xFFFFFFFF) {
      ; hBitmap converts alpha channel to specified alpha color.
      ; Adds 1 pixel because Anti-Alias (SmoothingMode = 4)
      ; Should it be crop 1 pixel instead?
			pBitmap := this.Bitmap()
			hBitmap := Gdip_CreateHBITMAPFromBitmap(pBitmap, alpha)
			Gdip_DisposeImage(pBitmap)
			return hBitmap
		}
		
		Save(filename := "", quality := 92) {
			filename := (filename ~= "i)\.(bmp|dib|rle|jpg|jpeg|jpe|jfif|gif|tif|tiff|png)$") ? filename
               : (filename != "") ? filename ".png" : this.title ".png"
			pBitmap := this.Bitmap()
			Gdip_SaveBitmapToFile(pBitmap, filename, quality)
			Gdip_DisposeImage(pBitmap)
			return this
		}
		
		Screenshot(filename := "", quality := 92) {
			filename := (filename ~= "i)\.(bmp|dib|rle|jpg|jpeg|jpe|jfif|gif|tif|tiff|png)$") ? filename
               : (filename != "") ? filename ".png" : this.title ".png"
			pBitmap := Gdip_BitmapFromScreen(this.x "|" this.y "|" this.xx - this.x "|" this.yy - this.y)
			Gdip_SaveBitmapToFile(pBitmap, filename, quality)
			Gdip_DisposeImage(pBitmap)
			return this
		}
		
		Render(text := "", style1 := "", style2 := "", update := true) {
			if !(this.hwnd){
				_subtitle := (this.outer) ? new this.outer.Subtitle() : new Subtitle()
				return _subtitle.Render(text, style1, style2, update)
			}
			else {
				Critical On
				this.Draw(text, style1, style2)
				this.DetectScreenResolutionChange()
				if (this.allowDrag == true)
					this.Reposition()
				if (update == true) {
					UpdateLayeredWindow(this.hwnd, this.hdc, 0, 0, this.ScreenWidth, this.ScreenHeight)
				}
				if (this.time) {
					self_destruct := ObjBindMethod(this, "Destroy")
					SetTimer, % self_destruct, % -1 * this.time
				}
				this.rendered := true
				Critical Off
				return this
			}
		}
		
		RenderToBitmap(text := "", style1 := "", style2 := "") {
			if !(this.hwnd){
				_subtitle := (this.outer) ? new this.outer.Subtitle() : new Subtitle()
				return _subtitle.RenderToBitmap(text, style1, style2)
			} else {
				this.Render(text, style1, style2, false)
				return this.Bitmap()
			}
		}
		
		RenderToHBitmap(text := "", style1 := "", style2 := "") {
			if !(this.hwnd){
				_subtitle := (this.outer) ? new this.outer.Subtitle() : new Subtitle()
				return _subtitle.RenderToHBitmap(text, style1, style2)
			} else {
				this.Render(text, style1, style2, false)
				return this.hBitmap()
			}
		}
		
		Reposition() {
			CoordMode, Mouse, Screen
			MouseGetPos, x_mouse, y_mouse
			this.LButton := GetKeyState("LButton", "P") ? 1 : 0
			this.keypress := (this.LButton && DllCall("GetForegroundWindow") == this.hwnd) ? ((!this.keypress) ? 1 : -1) : ((this.keypress == -1) ? 2 : 	0)
			
			if (this.keypress == 1) {
				this.x_mouse := x_mouse, this.y_mouse := y_mouse
				this.hbm2 := CreateDIBSection(A_ScreenWidth, A_ScreenHeight)
				this.hdc2 := CreateCompatibleDC()
				this.obm2 := SelectObject(this.hdc2, this.hbm2)
				this.G2 := Gdip_GraphicsFromHDC(this.hdc2)
			}
			
			if (this.keypress == -1) {
				dx := x_mouse - this.x_mouse
				dy := y_mouse - this.y_mouse
				safe_x := (0 + dx <= 0) ? 0 : 0 + dx
				safe_y := (0 + dy <= 0) ? 0 : 0 + dy
				safe_w := (0 + this.ScreenWidth + dx >= this.ScreenWidth) ? this.ScreenWidth : 0 + this.ScreenWidth + dx
				safe_h := (0 + this.ScreenHeight + dy >= this.ScreenHeight) ? this.ScreenHeight : 0 + this.ScreenHeight + dy
				source_x := (dx < 0) ? -dx : 0
				source_y := (dy < 0) ? -dy : 0
         ;Tooltip % dx ", " dy "`n" safe_x ", " safe_y ", " safe_w ", " safe_h
				Gdip_GraphicsClear(this.G2)
				BitBlt(this.hdc2, safe_x, safe_y, safe_w, safe_h, this.hdc, source_x, source_y)
				UpdateLayeredWindow(this.hwnd, this.hdc2, 0, 0, this.ScreenWidth, this.ScreenHeight)
			}
			
			if (this.keypress == 2) {
				Gdip_DeleteGraphics(this.G)
				SelectObject(this.hdc, this.obm)
				DeleteObject(this.hbm)
				DeleteDC(this.hdc)
				this.hdc := this.hdc2
				this.obm := this.obm2
				this.hbm := this.hbm2
				this.G := Gdip_GraphicsFromHDC(this.hdc2)
			}
			
			Reposition := ObjBindMethod(this, "Reposition")
			SetTimer, % Reposition, -10
		}
		
		Draw(text := "", style1 := "", style2 := "", pGraphics := "") {
      ; If the image was previously rendered, reset everything like a new Subtitle object.
			if (pGraphics == "") {
				if (this.rendered == true) {
					this.rendered := false
					this.layers := {}
					this.x := this.y := this.xx := this.yy := "" ; not 0!
					Gdip_GraphicsClear(this.G)
				}
				this.layers.push([text, style1, style2]) ; Saves each call of Draw()
				pGraphics := this.G
			}
			
      ; Remove excess whitespace. This is required for proper RegEx detection.
			style1 := !IsObject(style1) ? RegExReplace(style1, "\s+", " ") : style1
			style2 := !IsObject(style2) ? RegExReplace(style2, "\s+", " ") : style2
			
      ; Load saved styles if and only if both styles are blank.
			if (style1 == "" && style2 == "")
				style1 := this.style1, style2 := this.style2
			else
				this.style1 := style1, this.style2 := style2 ; Remember styles so that they can be loaded next time.
			
      ; RegEx help? https://regex101.com/r/xLzZzO/2
			static q1 := "(?i)^.*?\b(?<!:|:\s)\b"
			static q2 := "(?!(?>\([^()]*\)|[^()]*)*\))(:\s*)?\(?(?<value>(?<=\()([\s:#%_a-z\-\.\d]+|\([\s:#%_a-z\-\.\d]*\))*(?=\))|[#%_a-z\-\.\d]+).*$"
			
      ; Extract the time variable and save it for later when we Render() everything.
			this.time := (style1.time) ? style1.time : (style1.t) ? style1.t
         : (!IsObject(style1) && (___ := RegExReplace(style1, q1 "(t(ime)?)" q2, "${value}")) != style1) ? ___
         : (style2.time) ? style2.time : (style2.t) ? style2.t
         : (!IsObject(style2) && (___ := RegExReplace(style2, q1 "(t(ime)?)" q2, "${value}")) != style2) ? ___
         : this.time
			
      ; Extract styles from the background styles parameter.
			if IsObject(style1) {
				_a  := (style1.anchor != "")   ? style1.anchor   : style1.a
				_x  := (style1.left != "")     ? style1.left     : style1.x
				_y  := (style1.top != "")      ? style1.top      : style1.y
				_w  := (style1.width != "")    ? style1.width    : style1.w
				_h  := (style1.height != "")   ? style1.height   : style1.h
				_r  := (style1.radius != "")   ? style1.radius   : style1.r
				_c  := (style1.color != "")    ? style1.color    : style1.c
				_m  := (style1.margin != "")   ? style1.margin   : style1.m
				_p  := (style1.padding != "")  ? style1.padding  : style1.p
				_q  := (style1.quality != "")  ? style1.quality  : (style1.q) ? style1.q : style1.SmoothingMode
			} else {
				_a  := ((___ := RegExReplace(style1, q1    "(a(nchor)?)"        q2, "${value}")) != style1) ? ___ : ""
				_x  := ((___ := RegExReplace(style1, q1    "(x|left)"           q2, "${value}")) != style1) ? ___ : ""
				_y  := ((___ := RegExReplace(style1, q1    "(y|top)"            q2, "${value}")) != style1) ? ___ : ""
				_w  := ((___ := RegExReplace(style1, q1    "(w(idth)?)"         q2, "${value}")) != style1) ? ___ : ""
				_h  := ((___ := RegExReplace(style1, q1    "(h(eight)?)"        q2, "${value}")) != style1) ? ___ : ""
				_r  := ((___ := RegExReplace(style1, q1    "(r(adius)?)"        q2, "${value}")) != style1) ? ___ : ""
				_c  := ((___ := RegExReplace(style1, q1    "(c(olor)?)"         q2, "${value}")) != style1) ? ___ : ""
				_m  := ((___ := RegExReplace(style1, q1    "(m(argin)?)"        q2, "${value}")) != style1) ? ___ : ""
				_p  := ((___ := RegExReplace(style1, q1    "(p(adding)?)"       q2, "${value}")) != style1) ? ___ : ""
				_q  := ((___ := RegExReplace(style1, q1    "(q(uality)?)"       q2, "${value}")) != style1) ? ___ : ""
			}
			
      ; Extract styles from the text styles parameter.
			if IsObject(style2) {
				a  := (style2.anchor != "")      ? style2.anchor      : style2.a
				x  := (style2.left != "")        ? style2.left        : style2.x
				y  := (style2.top != "")         ? style2.top         : style2.y
				w  := (style2.width != "")       ? style2.width       : style2.w
				h  := (style2.height != "")      ? style2.height      : style2.h
				m  := (style2.margin != "")      ? style2.margin      : style2.m
				f  := (style2.font != "")        ? style2.font        : style2.f
				s  := (style2.size != "")        ? style2.size        : style2.s
				c  := (style2.color != "")       ? style2.color       : style2.c
				b  := (style2.bold != "")        ? style2.bold        : style2.b
				i  := (style2.italic != "")      ? style2.italic      : style2.i
				u  := (style2.underline != "")   ? style2.underline   : style2.u
				j  := (style2.justify != "")     ? style2.justify     : style2.j
				n  := (style2.noWrap != "")      ? style2.noWrap      : style2.n
				z  := (style2.condensed != "")   ? style2.condensed   : style2.z
				d  := (style2.dropShadow != "")  ? style2.dropShadow  : style2.d
				o  := (style2.outline != "")     ? style2.outline     : style2.o
				q  := (style2.quality != "")     ? style2.quality     : (style2.q) ? style2.q : style2.TextRenderingHint
			} else {
				a  := ((___ := RegExReplace(style2, q1    "(a(nchor)?)"        q2, "${value}")) != style2) ? ___ : ""
				x  := ((___ := RegExReplace(style2, q1    "(x|left)"           q2, "${value}")) != style2) ? ___ : ""
				y  := ((___ := RegExReplace(style2, q1    "(y|top)"            q2, "${value}")) != style2) ? ___ : ""
				w  := ((___ := RegExReplace(style2, q1    "(w(idth)?)"         q2, "${value}")) != style2) ? ___ : ""
				h  := ((___ := RegExReplace(style2, q1    "(h(eight)?)"        q2, "${value}")) != style2) ? ___ : ""
				m  := ((___ := RegExReplace(style2, q1    "(m(argin)?)"        q2, "${value}")) != style2) ? ___ : ""
				f  := ((___ := RegExReplace(style2, q1    "(f(ont)?)"          q2, "${value}")) != style2) ? ___ : ""
				s  := ((___ := RegExReplace(style2, q1    "(s(ize)?)"          q2, "${value}")) != style2) ? ___ : ""
				c  := ((___ := RegExReplace(style2, q1    "(c(olor)?)"         q2, "${value}")) != style2) ? ___ : ""
				b  := ((___ := RegExReplace(style2, q1    "(b(old)?)"          q2, "${value}")) != style2) ? ___ : ""
				i  := ((___ := RegExReplace(style2, q1    "(i(talic)?)"        q2, "${value}")) != style2) ? ___ : ""
				u  := ((___ := RegExReplace(style2, q1    "(u(nderline)?)"     q2, "${value}")) != style2) ? ___ : ""
				j  := ((___ := RegExReplace(style2, q1    "(j(ustify)?)"       q2, "${value}")) != style2) ? ___ : ""
				n  := ((___ := RegExReplace(style2, q1    "(n(oWrap)?)"        q2, "${value}")) != style2) ? ___ : ""
				z  := ((___ := RegExReplace(style2, q1    "(z|condensed)"      q2, "${value}")) != style2) ? ___ : ""
				d  := ((___ := RegExReplace(style2, q1    "(d(ropShadow)?)"    q2, "${value}")) != style2) ? ___ : ""
				o  := ((___ := RegExReplace(style2, q1    "(o(utline)?)"       q2, "${value}")) != style2) ? ___ : ""
				q  := ((___ := RegExReplace(style2, q1    "(q(uality)?)"       q2, "${value}")) != style2) ? ___ : ""
			}
			
      ; These are the type checkers.
			static valid := "^\s*(\-?\d+(?:\.\d*)?)\s*(%|pt|px|vh|vmin|vw)?\s*$"
			static valid_positive := "^\s*(\d+(?:\.\d*)?)\s*(%|pt|px|vh|vmin|vw)?\s*$"
			
      ; Define viewport width and height. This is the visible screen area.
			this.vw := 0.01 * this.ScreenWidth    ; 1% of viewport width.
			this.vh := 0.01 * this.ScreenHeight   ; 1% of viewport height.
			this.vmin := (this.vw < this.vh) ? this.vw : this.vh ; 1vw or 1vh, whichever is smaller.
			
      ; Get Rendering Quality.
			_q := (_q >= 0 && _q <= 4) ? _q : 4          ; Default SmoothingMode is 4 if radius is set. See Draw 1.
			q  := (q >= 0 && q <= 5) ? q : 4             ; Default TextRenderingHint is 4 (antialias).
			
      ; Get Font size.
			s  := (s ~= valid_positive) ? RegExReplace(s, "\s", "") : "2.23vh"           ; Default font size is 2.23vh.
			s  := (s ~= "(pt|px)$") ? SubStr(s, 1, -2) : s                               ; Strip spaces, px, and pt.
			s  := (s ~= "vh$") ? RegExReplace(s, "vh$", "") * this.vh : s                ; Relative to viewport height.
			s  := (s ~= "vw$") ? RegExReplace(s, "vw$", "") * this.vw : s                ; Relative to viewport width.
			s  := (s ~= "(%|vmin)$") ? RegExReplace(s, "(%|vmin)$", "") * this.vmin : s  ; Relative to viewport minimum.
			
      ; Get Bold, Italic, Underline, NoWrap, and Justification of text.
			style += (b) ? 1 : 0         ; bold
			style += (i) ? 2 : 0         ; italic
			style += (u) ? 4 : 0         ; underline
			style += (strikeout) ? 8 : 0 ; strikeout, not implemented.
			n  := (n) ? 0x4000 | 0x1000 : 0x4000
			j  := (j ~= "i)cent(er|re)") ? 1 : (j ~= "i)(far|right)") ? 2 : 0   ; Left/near, center/centre, far/right.
			
      ; Later when text x and w are finalized and it is found that x + ReturnRC[3] exceeds the screen,
      ; then the _redrawBecauseOfCondensedFont flag is set to true.
			if (this._redrawBecauseOfCondensedFont == true)
				f:=z, z:=0, this._redrawBecauseOfCondensedFont := false
			
      ; Create Font.
			hFamily := (___ := Gdip_FontFamilyCreate(f)) ? ___ : Gdip_FontFamilyCreate("Arial") ; Default font is Arial.
			hFont := Gdip_FontCreate(hFamily, s, style)
			hFormat := Gdip_StringFormatCreate(n)
			Gdip_SetStringFormatAlign(hFormat, j)  ; Left = 0, Center = 1, Right = 2
			
      ; Simulate string width and height. This will get the exact width and height of the text.
			CreateRectF(RC, 0, 0, 0, 0)
			Gdip_SetSmoothingMode(pGraphics, _q)     ; None = 3, AntiAlias = 4
			Gdip_SetTextRenderingHint(pGraphics, q)  ; Anti-Alias = 4, Cleartype = 5 (and gives weird effects.)
			ReturnRC := Gdip_MeasureString(pGraphics, Text, hFont, hFormat, RC)
			ReturnRC := StrSplit(ReturnRC, "|")      ; Contains the values for measured x, y, w, h text.
			
      ; Get background width and height. Default width and height are simulated width and height.
			_w := (_w ~= valid_positive) ? RegExReplace(_w, "\s", "") : ReturnRC[3]
			_w := (_w ~= "(pt|px)$") ? SubStr(_w, 1, -2) : _w
			_w := (_w ~= "(%|vw)$") ? RegExReplace(_w, "(%|vw)$", "") * this.vw : _w
			_w := (_w ~= "vh$") ? RegExReplace(_w, "vh$", "") * this.vh : _w
			_w := (_w ~= "vmin$") ? RegExReplace(_w, "vmin$", "") * this.vmin : _w
      ; Output is a decimal with pixel units.
			
			_h := (_h ~= valid_positive) ? RegExReplace(_h, "\s", "") : ReturnRC[4]
			_h := (_h ~= "(pt|px)$") ? SubStr(_h, 1, -2) : _h
			_h := (_h ~= "vw$") ? RegExReplace(_h, "vw$", "") * this.vw : _h
			_h := (_h ~= "(%|vh)$") ? RegExReplace(_h, "(%|vh)$", "") * this.vh : _h
			_h := (_h ~= "vmin$") ? RegExReplace(_h, "vmin$", "") * this.vmin : _h
      ; Output is a decimal with pixel units.
			
      ; Get background anchor. This is where the origin of the image is located.
      ; The default origin is the top left corner. Default anchor is 1.
			_a := RegExReplace(_a, "\s", "")
			_a := (_a = "top") ? 2 : (_a = "left") ? 4 : (_a = "right") ? 6 : (_a = "bottom") ? 8
         : (_a ~= "i)top" && _a ~= "i)left") ? 1 : (_a ~= "i)top" && _a ~= "i)cent(er|re)") ? 2
         : (_a ~= "i)top" && _a ~= "i)bottom") ? 3 : (_a ~= "i)cent(er|re)" && _a ~= "i)left") ? 4
         : (_a ~= "i)cent(er|re)") ? 5 : (_a ~= "i)cent(er|re)" && _a ~= "i)bottom") ? 6
         : (_a ~= "i)bottom" && _a ~= "i)left") ? 7 : (_a ~= "i)bottom" && _a ~= "i)cent(er|re)") ? 8
         : (_a ~= "i)bottom" && _a ~= "i)right") ? 9 : (_a ~= "^[1-9]$") ? _a : 1 ; Default anchor is top-left.
			
      ; _x and _y can be specified as locations (left, center, right, top, bottom).
      ; These location words in _x and _y take precedence over the values in _a.
			_a  := (_x ~= "i)left") ? 1+(((_a-1)//3)*3) : (_x ~= "i)cent(er|re)") ? 2+(((_a-1)//3)*3) : (_x ~= "i)right") ? 3+(((_a-1)//3)*3) : _a
			_a  := (_y ~= "i)top") ? 1+(mod(_a-1,3)) : (_y ~= "i)cent(er|re)") ? 4+(mod(_a-1,3)) : (_y ~= "i)bottom") ? 7+(mod(_a-1,3)) : _a
			
      ; Convert English words to numbers. Don't mess with these values any further.
			_x  := (_x ~= "i)left") ? 0 : (_x ~= "i)cent(er|re)") ? 0.5*this.ScreenWidth : (_x ~= "i)right") ? this.ScreenWidth : _x
			_y  := (_y ~= "i)top") ? 0 : (_y ~= "i)cent(er|re)") ? 0.5*this.ScreenHeight : (_y ~= "i)bottom") ? this.ScreenHeight : _y
			
      ; Get _x value.
			_x := (_x ~= valid) ? RegExReplace(_x, "\s", "") : 0  ; Default _x is 0.
			_x := (_x ~= "(pt|px)$") ? SubStr(_x, 1, -2) : _x
			_x := (_x ~= "(%|vw)$") ? RegExReplace(_x, "(%|vw)$", "") * this.vw : _x
			_x := (_x ~= "vh$") ? RegExReplace(_x, "vh$", "") * this.vh : _x
			_x := (_x ~= "vmin$") ? RegExReplace(_x, "vmin$", "") * this.vmin : _x
			
      ; Get _y value.
			_y := (_y ~= valid) ? RegExReplace(_y, "\s", "") : 0  ; Default _y is 0.
			_y := (_y ~= "(pt|px)$") ? SubStr(_y, 1, -2) : _y
			_y := (_y ~= "vw$") ? RegExReplace(_y, "vw$", "") * this.vw : _y
			_y := (_y ~= "(%|vh)$") ? RegExReplace(_y, "(%|vh)$", "") * this.vh : _y
			_y := (_y ~= "vmin$") ? RegExReplace(_y, "vmin$", "") * this.vmin : _y
			
      ; Now let's modify the _x and _y values with the _anchor, so that the image has a new point of origin.
      ; We need our calculated _width and _height for this.
			_x  -= (mod(_a-1,3) == 0) ? 0 : (mod(_a-1,3) == 1) ? _w/2 : (mod(_a-1,3) == 2) ? _w : 0
			_y  -= (((_a-1)//3) == 0) ? 0 : (((_a-1)//3) == 1) ? _h/2 : (((_a-1)//3) == 2) ? _h : 0
      ; Fractional y values might cause gdi+ slowdown.
			
			
      ; Get the text width and text height.
      ; Note that there are two new lines. Matching a percent symbol (%) will give text width/height
      ; that is relative to the background width/height. This is undesirable behavior, and so
      ; the user should use "vh" and "vw" whenever possible.
			w  := ( w ~= valid_positive) ? RegExReplace( w, "\s", "") : ReturnRC[3] ; Default is simulated text width.
			w  := ( w ~= "(pt|px)$") ? SubStr( w, 1, -2) :  w
			w  := ( w ~= "vw$") ? RegExReplace( w, "vw$", "") * this.vw :  w
			w  := ( w ~= "vh$") ? RegExReplace( w, "vh$", "") * this.vh :  w
			w  := ( w ~= "vmin$") ? RegExReplace( w, "vmin$", "") * this.vmin :  w
			w  := ( w ~= "%$") ? RegExReplace( w, "%$", "") * 0.01 * _w :  w
			
			h  := ( h ~= valid_positive) ? RegExReplace( h, "\s", "") : ReturnRC[4] ; Default is simulated text height.
			h  := ( h ~= "(pt|px)$") ? SubStr( h, 1, -2) :  h
			h  := ( h ~= "vw$") ? RegExReplace( h, "vw$", "") * this.vw :  h
			h  := ( h ~= "vh$") ? RegExReplace( h, "vh$", "") * this.vh :  h
			h  := ( h ~= "vmin$") ? RegExReplace( h, "vmin$", "") * this.vmin :  h
			h  := ( h ~= "%$") ? RegExReplace( h, "%$", "") * 0.01 * _h :  h
			
      ; If text justification is set but x is not, align the justified text relative to the center
      ; or right of the backgound, after taking into account the text width.
			if (x == "")
				x  := (j = 1) ? _x + (_w/2) - (w/2) : (j = 2) ? _x + _w - w : x
			
      ; Get anchor.
			a  := RegExReplace( a, "\s", "")
			a  := (a = "top") ? 2 : (a = "left") ? 4 : (a = "right") ? 6 : (a = "bottom") ? 8
         : (a ~= "i)top" && a ~= "i)left") ? 1 : (a ~= "i)top" && a ~= "i)cent(er|re)") ? 2
         : (a ~= "i)top" && a ~= "i)bottom") ? 3 : (a ~= "i)cent(er|re)" && a ~= "i)left") ? 4
         : (a ~= "i)cent(er|re)") ? 5 : (a ~= "i)cent(er|re)" && a ~= "i)bottom") ? 6
         : (a ~= "i)bottom" && a ~= "i)left") ? 7 : (a ~= "i)bottom" && a ~= "i)cent(er|re)") ? 8
         : (a ~= "i)bottom" && a ~= "i)right") ? 9 : (a ~= "^[1-9]$") ? a : 1 ; Default anchor is top-left.
			
      ; Text x and text y can be specified as locations (left, center, right, top, bottom).
      ; These location words in text x and text y take precedence over the values in the text anchor.
			a  := ( x ~= "i)left") ? 1+((( a-1)//3)*3) : ( x ~= "i)cent(er|re)") ? 2+((( a-1)//3)*3) : ( x ~= "i)right") ? 3+((( a-1)//3)*3) :  a
			a  := ( y ~= "i)top") ? 1+(mod( a-1,3)) : ( y ~= "i)cent(er|re)") ? 4+(mod( a-1,3)) : ( y ~= "i)bottom") ? 7+(mod( a-1,3)) :  a
			
      ; Convert English words to numbers. Don't mess with these values any further.
      ; Also, these values are relative to the background.
			x  := ( x ~= "i)left") ? _x : (x ~= "i)cent(er|re)") ? _x + 0.5*_w : (x ~= "i)right") ? _x + _w : x
			y  := ( y ~= "i)top") ? _y : (y ~= "i)cent(er|re)") ? _y + 0.5*_h : (y ~= "i)bottom") ? _y + _h : y
			
      ; Validate text x and y, convert to pixels.
			x  := ( x ~= valid) ? RegExReplace( x, "\s", "") : _x ; Default text x is background x.
			x  := ( x ~= "(pt|px)$") ? SubStr( x, 1, -2) :  x
			x  := ( x ~= "vw$") ? RegExReplace( x, "vw$", "") * this.vw :  x
			x  := ( x ~= "vh$") ? RegExReplace( x, "vh$", "") * this.vh :  x
			x  := ( x ~= "vmin$") ? RegExReplace( x, "vmin$", "") * this.vmin :  x
			x  := ( x ~= "%$") ? RegExReplace( x, "%$", "") * 0.01 * _w :  x
			
			y  := ( y ~= valid) ? RegExReplace( y, "\s", "") : _y ; Default text y is background y.
			y  := ( y ~= "(pt|px)$") ? SubStr( y, 1, -2) :  y
			y  := ( y ~= "vw$") ? RegExReplace( y, "vw$", "") * this.vw :  y
			y  := ( y ~= "vh$") ? RegExReplace( y, "vh$", "") * this.vh :  y
			y  := ( y ~= "vmin$") ? RegExReplace( y, "vmin$", "") * this.vmin :  y
			y  := ( y ~= "%$") ? RegExReplace( y, "%$", "") * 0.01 * _h :  y
			
      ; Modify text x and text y values with the anchor, so that the text has a new point of origin.
      ; The text anchor is relative to the text width and height before margin/padding.
      ; This is NOT relative to the background width and height.
			x  -= (mod(a-1,3) == 0) ? 0 : (mod(a-1,3) == 1) ? w/2 : (mod(a-1,3) == 2) ? w : 0
			y  -= (((a-1)//3) == 0) ? 0 : (((a-1)//3) == 1) ? h/2 : (((a-1)//3) == 2) ? h : 0
			
      ; Define margin and padding. Both parameters will leave the text unchanged,
      ; expanding the background box. The difference between the two is NON-EXISTENT.
      ; What does matter is if the margin/padding is a background style, the position of the text will not change.
      ; If the margin/padding is a text style, the text position will change.
      ; THERE REALLY IS NO DIFFERENCE BETWEEN MARGIN AND PADDING.
			_p := this.margin_and_padding(_p)
			_m := this.margin_and_padding(_m)
			p  := this.margin_and_padding( p)
			m  := this.margin_and_padding( m)
			
      ; Modify _x, _y, _w, _h with margin and padding, increasing the size of the background.
			if (_w || _h) {
				_w  += (_m.2 + _m.4 + _p.2 + _p.4) + (m.2 + m.4 + p.2 + p.4)
				_h  += (_m.1 + _m.3 + _p.1 + _p.3) + (m.1 + m.3 + p.1 + p.3)
				_x  -= (_m.4 + _p.4)
				_y  -= (_m.1 + _p.1)
			}
			
      ; If margin/padding are defined in the text parameter, shift the position of the text.
			x  += (m.4 + p.4)
			y  += (m.1 + p.1)
			
      ; Re-run: Condense Text using a Condensed Font if simulated text width exceeds screen width.
			if (Gdip_FontFamilyCreate(z)) {
				if (ReturnRC[3] + x > this.ScreenWidth) {
					this._redrawBecauseOfCondensedFont := true
					return this.Draw(text, style1, style2, pGraphics)
				}
			}
			
      ; Define radius of rounded corners.
			_r := (_r ~= valid_positive) ? RegExReplace(_r, "\s", "") : 0  ; Default radius is 0, or square corners.
			_r := (_r ~= "(pt|px)$") ? SubStr(_r, 1, -2) : _r
			_r := (_r ~= "vw$") ? RegExReplace(_r, "vw$", "") * this.vw : _r
			_r := (_r ~= "vh$") ? RegExReplace(_r, "vh$", "") * this.vh : _r
			_r := (_r ~= "vmin$") ? RegExReplace(_r, "vmin$", "") * this.vmin : _r
      ; percentage is defined as a percentage of the smaller background width/height.
			_r := (_r ~= "%$") ? RegExReplace(_r, "%$", "") * 0.01 * ((_w > _h) ? _h : _w) : _r
      ; the radius cannot exceed the half width or half height, whichever is smaller.
			_r  := (_r <= ((_w > _h) ? _h : _w) / 2) ? _r : 0
			
      ; Define color.
			_c := this.color(_c, 0xDD424242) ; Default background color is transparent gray.
			SourceCopy := (c ~= "i)(delete|eraser?|overwrite|sourceCopy)") ? 1 : 0 ; Eraser brush for text.
			c  := (SourceCopy) ? 0x00000000 : this.color( c, 0xFFFFFFFF) ; Default text color is white.
			
      ; Define outline and dropShadow.
			o := this.outline(o, s, c)
			d := this.dropShadow(d, ReturnRC[3], ReturnRC[4], s)
			
      ; Round 9 - Define Text
			if (!A_IsUnicode){
				nSize := DllCall("MultiByteToWideChar", "uint",0, "uint",0, "ptr",&text, "int",-1, "ptr",0, "int",0)
				VarSetCapacity(wtext, nSize*2)
				DllCall("MultiByteToWideChar", "uint",0, "uint",0, "ptr",&text, "int",-1, "ptr",&wtext, "int",nSize)
			}
			
      ; Round 10 - Finalize _x, _y, _w, _h
			_x  := Round(_x)
			_y  := Round(_y)
			_w  := Round(_w)
			_h  := Round(_h)
			
      ; Define image boundaries using the background boundaries.
			this.x  := (this.x  = "" || _x < this.x) ? _x : this.x
			this.y  := (this.y  = "" || _y < this.y) ? _y : this.y
			this.xx := (this.xx = "" || _x + _w > this.xx) ? _x + _w : this.xx
			this.yy := (this.yy = "" || _y + _h > this.yy) ? _y + _h : this.yy
			
      ; Define image boundaries using the text boundaries + outline boundaries.
			artifacts := Ceil(o.3 + 0.5*o.1)
			this.x  := (x - artifacts < this.x) ? x - artifacts : this.x
			this.y  := (y - artifacts < this.y) ? y - artifacts : this.y
			this.xx := (x + w + artifacts > this.xx) ? x + w + artifacts : this.xx
			this.yy := (y + h + artifacts > this.yy) ? y + h + artifacts : this.yy
			
      ; Define image boundaries using the dropShadow boundaries.
			artifacts := Ceil(d.3 + d.6 + 0.5*o.1)
			this.x  := (x + d.1 - artifacts < this.x) ? x + d.1 - artifacts : this.x
			this.y  := (y + d.2 - artifacts < this.y) ? y + d.2 - artifacts : this.y
			this.xx := (x + d.1 + w + artifacts > this.xx) ? x + d.1 + w + artifacts : this.xx
			this.yy := (y + d.2 + h + artifacts > this.yy) ? y + d.2 + h + artifacts : this.yy
			
      ; Round to the nearest integer.
			this.x := Floor(this.x)
			this.y := Floor(this.y)
			this.xx := Ceil(this.xx)
			this.yy := Ceil(this.yy)
			
      ; Draw 1 - Background
			if (_w && _h && _c && (_c & 0xFF000000)) {
				if (_r == 0)
					Gdip_SetSmoothingMode(pGraphics, 1) ; Turn antialiasing off if not a rounded rectangle.
				pBrushBackground := Gdip_BrushCreateSolid(_c)
				Gdip_FillRoundedRectangle(pGraphics, pBrushBackground, _x, _y, _w, _h, _r) ; DRAWING!
				Gdip_DeleteBrush(pBrushBackground)
				if (_r == 0)
					Gdip_SetSmoothingMode(pGraphics, _q) ; Turn antialiasing on for text rendering.
			}
			
      ; Draw 2 - DropShadow
			if (!d.void) {
				offset2 := d.3 + d.6 + Ceil(0.5*o.1)
				
				if (d.3) {
					DropShadow := Gdip_CreateBitmap(w + 2*offset2, h + 2*offset2)
					DropShadowG := Gdip_GraphicsFromImage(DropShadow)
					Gdip_SetSmoothingMode(DropShadowG, _q)
					Gdip_SetTextRenderingHint(DropShadowG, q)
					CreateRectF(RC, offset2, offset2, w + 2*offset2, h + 2*offset2)
				} else {
					CreateRectF(RC, x + d.1, y + d.2, w, h)
					DropShadowG := pGraphics
				}
				
         ; Use Gdip_DrawString if and only if there is a horizontal/vertical offset.
				if (o.void && d.6 == 0)
				{
					pBrush := Gdip_BrushCreateSolid(d.4)
					Gdip_DrawString(DropShadowG, Text, hFont, hFormat, pBrush, RC) ; DRAWING!
					Gdip_DeleteBrush(pBrush)
				}
				else ; Otherwise, use the below code if blur, size, and opacity are set.
				{
            ; Draw the outer edge of the text string.
					DllCall("gdiplus\GdipCreatePath", "int",1, "uptr*",pPath)
					DllCall("gdiplus\GdipAddPathString", "ptr",pPath, "ptr", A_IsUnicode ? &text : &wtext, "int",-1
                                               , "ptr",hFamily, "int",style, "float",s, "ptr",&RC, "ptr",hFormat)
					pPen := Gdip_CreatePen(d.4, 2*d.6 + o.1)
					DllCall("gdiplus\GdipSetPenLineJoin", "ptr",pPen, "uint",2)
					DllCall("gdiplus\GdipDrawPath", "ptr",DropShadowG, "ptr",pPen, "ptr",pPath)
					Gdip_DeletePen(pPen)
					
            ; Fill in the outline. Turn off antialiasing and alpha blending so the gaps are 100% filled.
					pBrush := Gdip_BrushCreateSolid(d.4)
					Gdip_SetCompositingMode(DropShadowG, 1) ; Turn off alpha blending
					Gdip_SetSmoothingMode(DropShadowG, 3)   ; Turn off anti-aliasing
					Gdip_FillPath(DropShadowG, pBrush, pPath)
					Gdip_DeleteBrush(pBrush)
					Gdip_DeletePath(pPath)
					Gdip_SetCompositingMode(DropShadowG, 0)
					Gdip_SetSmoothingMode(DropShadowG, _q)
				}
				
				if (d.3) {
					Gdip_DeleteGraphics(DropShadowG)
					this.GaussianBlur(DropShadow, d.3, d.5)
					Gdip_SetInterpolationMode(pGraphics, 5) ; NearestNeighbor
					Gdip_SetSmoothingMode(pGraphics, 3) ; Turn off anti-aliasing
					Gdip_DrawImage(pGraphics, DropShadow, x + d.1 - offset2, y + d.2 - offset2, w + 2*offset2, h + 2*offset2) ; DRAWING!
					Gdip_SetSmoothingMode(pGraphics, _q)
					Gdip_DisposeImage(DropShadow)
				}
			}
			
      ; Draw 3 - Text Outline
			if (!o.void) {
         ; Convert our text to a path.
				CreateRectF(RC, x, y, w, h)
				DllCall("gdiplus\GdipCreatePath", "int",1, "uptr*",pPath)
				DllCall("gdiplus\GdipAddPathString", "ptr",pPath, "ptr", A_IsUnicode ? &text : &wtext, "int",-1
                                            , "ptr",hFamily, "int",style, "float",s, "ptr",&RC, "ptr",hFormat)
				
         ; Create a glow effect around the edges.
				if (o.3) {
					Gdip_SetClipPath(pGraphics, pPath, 3) ; Exclude original text region from being drawn on.
					pPenGlow := Gdip_CreatePen(Format("0x{:02X}",((o.4 & 0xFF000000) >> 24)/o.3) . Format("{:06X}",(o.4 & 0x00FFFFFF)), 1)
					DllCall("gdiplus\GdipSetPenLineJoin", "ptr",pPenGlow, "uint",2)
					
					loop % o.3
					{
						DllCall("gdiplus\GdipSetPenWidth", "ptr",pPenGlow, "float",o.1 + 2*A_Index)
						DllCall("gdiplus\GdipDrawPath", "ptr",pGraphics, "ptr",pPenGlow, "ptr",pPath) ; DRAWING!
					}
					Gdip_DeletePen(pPenGlow)
					Gdip_ResetClip(pGraphics)
				}
				
         ; Draw outline text.
				if (o.1) {
					pPen := Gdip_CreatePen(o.2, o.1)
					DllCall("gdiplus\GdipSetPenLineJoin", "ptr",pPen, "uint",2)
					DllCall("gdiplus\GdipDrawPath", "ptr",pGraphics, "ptr",pPen, "ptr",pPath) ; DRAWING!
					Gdip_DeletePen(pPen)
				}
				
         ; Fill outline text.
				pBrush := Gdip_BrushCreateSolid(c)
				Gdip_SetCompositingMode(pGraphics, SourceCopy)
				Gdip_FillPath(pGraphics, pBrush, pPath) ; DRAWING!
				Gdip_SetCompositingMode(pGraphics, 0)
				Gdip_DeleteBrush(pBrush)
				Gdip_DeletePath(pPath)
			}
			
      ; Draw Text if outline is not are not specified.
			if (text != "" && o.void) {
				CreateRectF(RC, x, y, w, h)
				pBrushText := Gdip_BrushCreateSolid(c)
				Gdip_SetCompositingMode(pGraphics, SourceCopy)
				Gdip_DrawString(pGraphics, A_IsUnicode ? text : wtext, hFont, hFormat, pBrushText, RC) ; DRAWING!
				Gdip_SetCompositingMode(pGraphics, 0)
				Gdip_DeleteBrush(pBrushText)
			}
			
      ; Delete Font Objects.
			Gdip_DeleteStringFormat(hFormat)
			Gdip_DeleteFont(hFont)
			Gdip_DeleteFontFamily(hFamily)
			
			return (pGraphics == "") ? this : ""
		}
		
		color(c, default := 0xDD424242) {
			static colorRGB  := "^0x([0-9A-Fa-f]{6})$"
			static colorARGB := "^0x([0-9A-Fa-f]{8})$"
			static hex6      :=   "^([0-9A-Fa-f]{6})$"
			static hex8      :=   "^([0-9A-Fa-f]{8})$"
			
			if ObjGetCapacity([c], 1) {
				c  := (c ~= "^#") ? SubStr(c, 2) : c
				c  := ((___ := this.colorMap(c)) != "") ? ___ : c
				c  := (c ~= colorRGB) ? "0xFF" RegExReplace(c, colorRGB, "$1") : (c ~= hex8) ? "0x" c : (c ~= hex6) ? "0xFF" c : c
				c  := (c ~= colorARGB) ? c : default
			}
			return (c != "") ? c : default
		}
		
		dropShadow(d, x_simulated, y_simulated, font_size) {
			static valid := "^\s*(\-?\d+(?:\.\d*)?)\s*(%|pt|px|vh|vmin|vw)?\s*$"
			static q1 := "(?i)^.*?\b(?<!:|:\s)\b"
			static q2 := "(?!(?>\([^()]*\)|[^()]*)*\))(:\s*)?\(?(?<value>(?<=\()([\s:#%_a-z\-\.\d]+|\([\s:#%_a-z\-\.\d]*\))*(?=\))|[#%_a-z\-\.\d]+).*$"
			
			if IsObject(d) {
				d.1 := (d.1) ? d.1 : (d.horizontal != "") ? d.horizontal : d.h
				d.2 := (d.2) ? d.2 : (d.vertical   != "") ? d.vertical   : d.v
				d.3 := (d.3) ? d.3 : (d.blur       != "") ? d.blur       : d.b
				d.4 := (d.4) ? d.4 : (d.color      != "") ? d.color      : d.c
				d.5 := (d.5) ? d.5 : (d.opacity    != "") ? d.opacity    : d.o
				d.6 := (d.6) ? d.6 : (d.size       != "") ? d.size       : d.s
			} else if (d != "") {
				_ := RegExReplace(d, ":\s+", ":")
				_ := RegExReplace(_, "\s+", " ")
				_ := StrSplit(_, " ")
				_.1 := ((___ := RegExReplace(d, q1    "(h(orizontal)?)"    q2, "${value}")) != d) ? ___ : _.1
				_.2 := ((___ := RegExReplace(d, q1    "(v(ertical)?)"      q2, "${value}")) != d) ? ___ : _.2
				_.3 := ((___ := RegExReplace(d, q1    "(b(lur)?)"          q2, "${value}")) != d) ? ___ : _.3
				_.4 := ((___ := RegExReplace(d, q1    "(c(olor)?)"         q2, "${value}")) != d) ? ___ : _.4
				_.5 := ((___ := RegExReplace(d, q1    "(o(pacity)?)"       q2, "${value}")) != d) ? ___ : _.5
				_.6 := ((___ := RegExReplace(d, q1    "(s(ize)?)"          q2, "${value}")) != d) ? ___ : _.6
				d := _
			}
			else return {"void":true, 1:0, 2:0, 3:0, 4:0, 5:0, 6:0}
				
			for key, value in d {
				if (key = 4) ; Don't mess with color data.
					continue
				d[key] := (d[key] ~= valid) ? RegExReplace(d[key], "\s", "") : 0 ; Default for everything is 0.
				d[key] := (d[key] ~= "(pt|px)$") ? SubStr(d[key], 1, -2) : d[key]
				d[key] := (d[key] ~= "vw$") ? RegExReplace(d[key], "vw$", "") * this.vw : d[key]
				d[key] := (d[key] ~= "vh$") ? RegExReplace(d[key], "vh$", "") * this.vh : d[key]
				d[key] := (d[key] ~= "vmin$") ? RegExReplace(d[key], "vmin$", "") * this.vmin : d[key]
			}
			
			d.1 := (d.1 ~= "%$") ? SubStr(d.1, 1, -1) * 0.01 * x_simulated : d.1
			d.2 := (d.2 ~= "%$") ? SubStr(d.2, 1, -1) * 0.01 * y_simulated : d.2
			d.3 := (d.3 ~= "%$") ? SubStr(d.3, 1, -1) * 0.01 * font_size : d.3
			d.4 := this.color(d.4, 0xFFFF0000) ; Default color is red.
			d.5 := (d.5 ~= "%$") ? SubStr(d.5, 1, -1) / 100 : d.5
			d.5 := (d.5 <= 0 || d.5 > 1) ? 1 : d.5 ; Range Opacity is a float from 0-1.
			d.6 := (d.6 ~= "%$") ? SubStr(d.6, 1, -1) * 0.01 * font_size : d.6
			return d
		}
		
		font(f, default := "Arial"){
			
		}
		
		margin_and_padding(m, default := 0) {
			static valid := "^\s*(\-?\d+(?:\.\d*)?)\s*(%|pt|px|vh|vmin|vw)?\s*$"
			static q1 := "(?i)^.*?\b(?<!:|:\s)\b"
			static q2 := "(?!(?>\([^()]*\)|[^()]*)*\))(:\s*)?\(?(?<value>(?<=\()([\s:#%_a-z\-\.\d]+|\([\s:#%_a-z\-\.\d]*\))*(?=\))|[#%_a-z\-\.\d]+).*$"
			
			if IsObject(m) {
				m.1 := (m.top    != "") ? m.top    : m.t
				m.2 := (m.right  != "") ? m.right  : m.r
				m.3 := (m.bottom != "") ? m.bottom : m.b
				m.4 := (m.left   != "") ? m.left   : m.l
			} else if (m != "") {
				_ := RegExReplace(m, ":\s+", ":")
				_ := RegExReplace(_, "\s+", " ")
				_ := StrSplit(_, " ")
				_.1 := ((___ := RegExReplace(m, q1    "(t(op)?)"           q2, "${value}")) != m) ? ___ : _.1
				_.2 := ((___ := RegExReplace(m, q1    "(r(ight)?)"         q2, "${value}")) != m) ? ___ : _.2
				_.3 := ((___ := RegExReplace(m, q1    "(b(ottom)?)"        q2, "${value}")) != m) ? ___ : _.3
				_.4 := ((___ := RegExReplace(m, q1    "(l(eft)?)"          q2, "${value}")) != m) ? ___ : _.4
				m := _
			}
			else return {1:default, 2:default, 3:default, 4:default}
				
      ; Follow CSS guidelines for margin!
			if (m.2 == "" && m.3 == "" && m.4 == "")
				m.4 := m.3 := m.2 := m.1, exception := true
			if (m.3 == "" && m.4 == "")
				m.4 := m.2, m.3 := m.1
			if (m.4 == "")
				m.4 := m.2
			
			for key, value in m {
				m[key] := (m[key] ~= valid) ? RegExReplace(m[key], "\s", "") : default
				m[key] := (m[key] ~= "(pt|px)$") ? SubStr(m[key], 1, -2) : m[key]
				m[key] := (m[key] ~= "vw$") ? RegExReplace(m[key], "vw$", "") * this.vw : m[key]
				m[key] := (m[key] ~= "vh$") ? RegExReplace(m[key], "vh$", "") * this.vh : m[key]
				m[key] := (m[key] ~= "vmin$") ? RegExReplace(m[key], "vmin$", "") * this.vmin : m[key]
			}
			m.1 := (m.1 ~= "%$") ? SubStr(m.1, 1, -1) * this.vh : m.1
			m.2 := (m.2 ~= "%$") ? SubStr(m.2, 1, -1) * (exception ? this.vh : this.vw) : m.2
			m.3 := (m.3 ~= "%$") ? SubStr(m.3, 1, -1) * this.vh : m.3
			m.4 := (m.4 ~= "%$") ? SubStr(m.4, 1, -1) * (exception ? this.vh : this.vw) : m.4
			return m
		}
		
		outline(o, font_size, font_color) {
			static valid_positive := "^\s*(\d+(?:\.\d*)?)\s*(%|pt|px|vh|vmin|vw)?\s*$"
			static q1 := "(?i)^.*?\b(?<!:|:\s)\b"
			static q2 := "(?!(?>\([^()]*\)|[^()]*)*\))(:\s*)?\(?(?<value>(?<=\()([\s:#%_a-z\-\.\d]+|\([\s:#%_a-z\-\.\d]*\))*(?=\))|[#%_a-z\-\.\d]+).*$"
			
			if IsObject(o) {
				o.1 := (o.1) ? o.1 : (o.stroke != "") ? o.stroke : o.s
				o.2 := (o.2) ? o.2 : (o.color  != "") ? o.color  : o.c
				o.3 := (o.3) ? o.3 : (o.glow   != "") ? o.glow   : o.g
				o.4 := (o.4) ? o.4 : (o.tint   != "") ? o.tint   : o.t
			} else if (o != "") {
				_ := RegExReplace(o, ":\s+", ":")
				_ := RegExReplace(_, "\s+", " ")
				_ := StrSplit(_, " ")
				_.1 := ((___ := RegExReplace(o, q1    "(s(troke)?)"        q2, "${value}")) != o) ? ___ : _.1
				_.2 := ((___ := RegExReplace(o, q1    "(c(olor)?)"         q2, "${value}")) != o) ? ___ : _.2
				_.3 := ((___ := RegExReplace(o, q1    "(g(low)?)"          q2, "${value}")) != o) ? ___ : _.3
				_.4 := ((___ := RegExReplace(o, q1    "(t(int)?)"          q2, "${value}")) != o) ? ___ : _.4
				o := _
			}
			else return {"void":true, 1:0, 2:0, 3:0, 4:0}
				
			for key, value in o {
				if (key = 2) || (key = 4) ; Don't mess with color data.
					continue
				o[key] := (o[key] ~= valid_positive) ? RegExReplace(o[key], "\s", "") : 0 ; Default for everything is 0.
				o[key] := (o[key] ~= "(pt|px)$") ? SubStr(o[key], 1, -2) : o[key]
				o[key] := (o[key] ~= "vw$") ? RegExReplace(o[key], "vw$", "") * this.vw : o[key]
				o[key] := (o[key] ~= "vh$") ? RegExReplace(o[key], "vh$", "") * this.vh : o[key]
				o[key] := (o[key] ~= "vmin$") ? RegExReplace(o[key], "vmin$", "") * this.vmin : o[key]
			}
			
			o.1 := (o.1 ~= "%$") ? SubStr(o.1, 1, -1) * 0.01 * font_size : o.1
			o.2 := this.color(o.2, font_color) ; Default color is the text font color.
			o.3 := (o.3 ~= "%$") ? SubStr(o.3, 1, -1) * 0.01 * font_size : o.3
			o.4 := this.color(o.4, o.2) ; Default color is outline color.
			return o
		}
		
		colorMap(c) {
			static map
			
			if !(map) {
				color := [] ; 73 LINES MAX
				color["Clear"] := color["Off"] := color["None"] := color["Transparent"] := "0x00000000"
				
				color["AliceBlue"]             := "0xFFF0F8FF"
      ,  color["AntiqueWhite"]          := "0xFFFAEBD7"
      ,  color["Aqua"]                  := "0xFF00FFFF"
      ,  color["Aquamarine"]            := "0xFF7FFFD4"
      ,  color["Azure"]                 := "0xFFF0FFFF"
      ,  color["Beige"]                 := "0xFFF5F5DC"
      ,  color["Bisque"]                := "0xFFFFE4C4"
      ,  color["Black"]                 := "0xFF000000"
      ,  color["BlanchedAlmond"]        := "0xFFFFEBCD"
      ,  color["Blue"]                  := "0xFF0000FF"
      ,  color["BlueViolet"]            := "0xFF8A2BE2"
      ,  color["Brown"]                 := "0xFFA52A2A"
      ,  color["BurlyWood"]             := "0xFFDEB887"
      ,  color["CadetBlue"]             := "0xFF5F9EA0"
      ,  color["Chartreuse"]            := "0xFF7FFF00"
      ,  color["Chocolate"]             := "0xFFD2691E"
      ,  color["Coral"]                 := "0xFFFF7F50"
      ,  color["CornflowerBlue"]        := "0xFF6495ED"
      ,  color["Cornsilk"]              := "0xFFFFF8DC"
      ,  color["Crimson"]               := "0xFFDC143C"
      ,  color["Cyan"]                  := "0xFF00FFFF"
      ,  color["DarkBlue"]              := "0xFF00008B"
      ,  color["DarkCyan"]              := "0xFF008B8B"
      ,  color["DarkGoldenRod"]         := "0xFFB8860B"
      ,  color["DarkGray"]              := "0xFFA9A9A9"
      ,  color["DarkGrey"]              := "0xFFA9A9A9"
      ,  color["DarkGreen"]             := "0xFF006400"
      ,  color["DarkKhaki"]             := "0xFFBDB76B"
      ,  color["DarkMagenta"]           := "0xFF8B008B"
      ,  color["DarkOliveGreen"]        := "0xFF556B2F"
      ,  color["DarkOrange"]            := "0xFFFF8C00"
      ,  color["DarkOrchid"]            := "0xFF9932CC"
      ,  color["DarkRed"]               := "0xFF8B0000"
      ,  color["DarkSalmon"]            := "0xFFE9967A"
      ,  color["DarkSeaGreen"]          := "0xFF8FBC8F"
      ,  color["DarkSlateBlue"]         := "0xFF483D8B"
      ,  color["DarkSlateGray"]         := "0xFF2F4F4F"
      ,  color["DarkSlateGrey"]         := "0xFF2F4F4F"
      ,  color["DarkTurquoise"]         := "0xFF00CED1"
      ,  color["DarkViolet"]            := "0xFF9400D3"
      ,  color["DeepPink"]              := "0xFFFF1493"
      ,  color["DeepSkyBlue"]           := "0xFF00BFFF"
      ,  color["DimGray"]               := "0xFF696969"
      ,  color["DimGrey"]               := "0xFF696969"
      ,  color["DodgerBlue"]            := "0xFF1E90FF"
      ,  color["FireBrick"]             := "0xFFB22222"
      ,  color["FloralWhite"]           := "0xFFFFFAF0"
      ,  color["ForestGreen"]           := "0xFF228B22"
      ,  color["Fuchsia"]               := "0xFFFF00FF"
      ,  color["Gainsboro"]             := "0xFFDCDCDC"
      ,  color["GhostWhite"]            := "0xFFF8F8FF"
      ,  color["Gold"]                  := "0xFFFFD700"
      ,  color["GoldenRod"]             := "0xFFDAA520"
      ,  color["Gray"]                  := "0xFF808080"
      ,  color["Grey"]                  := "0xFF808080"
      ,  color["Green"]                 := "0xFF008000"
      ,  color["GreenYellow"]           := "0xFFADFF2F"
      ,  color["HoneyDew"]              := "0xFFF0FFF0"
      ,  color["HotPink"]               := "0xFFFF69B4"
      ,  color["IndianRed"]             := "0xFFCD5C5C"
      ,  color["Indigo"]                := "0xFF4B0082"
      ,  color["Ivory"]                 := "0xFFFFFFF0"
      ,  color["Khaki"]                 := "0xFFF0E68C"
      ,  color["Lavender"]              := "0xFFE6E6FA"
      ,  color["LavenderBlush"]         := "0xFFFFF0F5"
      ,  color["LawnGreen"]             := "0xFF7CFC00"
      ,  color["LemonChiffon"]          := "0xFFFFFACD"
      ,  color["LightBlue"]             := "0xFFADD8E6"
      ,  color["LightCoral"]            := "0xFFF08080"
      ,  color["LightCyan"]             := "0xFFE0FFFF"
      ,  color["LightGoldenRodYellow"]  := "0xFFFAFAD2"
      ,  color["LightGray"]             := "0xFFD3D3D3"
      ,  color["LightGrey"]             := "0xFFD3D3D3"
				color["LightGreen"]            := "0xFF90EE90"
      ,  color["LightPink"]             := "0xFFFFB6C1"
      ,  color["LightSalmon"]           := "0xFFFFA07A"
      ,  color["LightSeaGreen"]         := "0xFF20B2AA"
      ,  color["LightSkyBlue"]          := "0xFF87CEFA"
      ,  color["LightSlateGray"]        := "0xFF778899"
      ,  color["LightSlateGrey"]        := "0xFF778899"
      ,  color["LightSteelBlue"]        := "0xFFB0C4DE"
      ,  color["LightYellow"]           := "0xFFFFFFE0"
      ,  color["Lime"]                  := "0xFF00FF00"
      ,  color["LimeGreen"]             := "0xFF32CD32"
      ,  color["Linen"]                 := "0xFFFAF0E6"
      ,  color["Magenta"]               := "0xFFFF00FF"
      ,  color["Maroon"]                := "0xFF800000"
      ,  color["MediumAquaMarine"]      := "0xFF66CDAA"
      ,  color["MediumBlue"]            := "0xFF0000CD"
      ,  color["MediumOrchid"]          := "0xFFBA55D3"
      ,  color["MediumPurple"]          := "0xFF9370DB"
      ,  color["MediumSeaGreen"]        := "0xFF3CB371"
      ,  color["MediumSlateBlue"]       := "0xFF7B68EE"
      ,  color["MediumSpringGreen"]     := "0xFF00FA9A"
      ,  color["MediumTurquoise"]       := "0xFF48D1CC"
      ,  color["MediumVioletRed"]       := "0xFFC71585"
      ,  color["MidnightBlue"]          := "0xFF191970"
      ,  color["MintCream"]             := "0xFFF5FFFA"
      ,  color["MistyRose"]             := "0xFFFFE4E1"
      ,  color["Moccasin"]              := "0xFFFFE4B5"
      ,  color["NavajoWhite"]           := "0xFFFFDEAD"
      ,  color["Navy"]                  := "0xFF000080"
      ,  color["OldLace"]               := "0xFFFDF5E6"
      ,  color["Olive"]                 := "0xFF808000"
      ,  color["OliveDrab"]             := "0xFF6B8E23"
      ,  color["Orange"]                := "0xFFFFA500"
      ,  color["OrangeRed"]             := "0xFFFF4500"
      ,  color["Orchid"]                := "0xFFDA70D6"
      ,  color["PaleGoldenRod"]         := "0xFFEEE8AA"
      ,  color["PaleGreen"]             := "0xFF98FB98"
      ,  color["PaleTurquoise"]         := "0xFFAFEEEE"
      ,  color["PaleVioletRed"]         := "0xFFDB7093"
      ,  color["PapayaWhip"]            := "0xFFFFEFD5"
      ,  color["PeachPuff"]             := "0xFFFFDAB9"
      ,  color["Peru"]                  := "0xFFCD853F"
      ,  color["Pink"]                  := "0xFFFFC0CB"
      ,  color["Plum"]                  := "0xFFDDA0DD"
      ,  color["PowderBlue"]            := "0xFFB0E0E6"
      ,  color["Purple"]                := "0xFF800080"
      ,  color["RebeccaPurple"]         := "0xFF663399"
      ,  color["Red"]                   := "0xFFFF0000"
      ,  color["RosyBrown"]             := "0xFFBC8F8F"
      ,  color["RoyalBlue"]             := "0xFF4169E1"
      ,  color["SaddleBrown"]           := "0xFF8B4513"
      ,  color["Salmon"]                := "0xFFFA8072"
      ,  color["SandyBrown"]            := "0xFFF4A460"
      ,  color["SeaGreen"]              := "0xFF2E8B57"
      ,  color["SeaShell"]              := "0xFFFFF5EE"
      ,  color["Sienna"]                := "0xFFA0522D"
      ,  color["Silver"]                := "0xFFC0C0C0"
      ,  color["SkyBlue"]               := "0xFF87CEEB"
      ,  color["SlateBlue"]             := "0xFF6A5ACD"
      ,  color["SlateGray"]             := "0xFF708090"
      ,  color["SlateGrey"]             := "0xFF708090"
      ,  color["Snow"]                  := "0xFFFFFAFA"
      ,  color["SpringGreen"]           := "0xFF00FF7F"
      ,  color["SteelBlue"]             := "0xFF4682B4"
      ,  color["Tan"]                   := "0xFFD2B48C"
      ,  color["Teal"]                  := "0xFF008080"
      ,  color["Thistle"]               := "0xFFD8BFD8"
      ,  color["Tomato"]                := "0xFFFF6347"
      ,  color["Turquoise"]             := "0xFF40E0D0"
      ,  color["Violet"]                := "0xFFEE82EE"
      ,  color["Wheat"]                 := "0xFFF5DEB3"
      ,  color["White"]                 := "0xFFFFFFFF"
      ,  color["WhiteSmoke"]            := "0xFFF5F5F5"
				color["Yellow"]                := "0xFFFFFF00"
      ,  color["YellowGreen"]           := "0xFF9ACD32"
				map := color
			}
			
			return map[c]
		}
		
		GaussianBlur(ByRef pBitmap, radius, opacity := 1) {
			static x86 := "
      (LTrim
      VYnlV1ZTg+xci0Uci30c2UUgx0WsAwAAAI1EAAGJRdiLRRAPr0UYicOJRdSLRRwP
      r/sPr0UYiX2ki30UiUWoi0UQjVf/i30YSA+vRRgDRQgPr9ONPL0SAAAAiUWci0Uc
      iX3Eg2XE8ECJVbCJRcCLRcSJZbToAAAAACnEi0XEiWXk6AAAAAApxItFxIllzOgA
      AAAAKcSLRaiJZcjHRdwAAAAAx0W8AAAAAIlF0ItFvDtFFA+NcAEAAItV3DHAi12c
      i3XQiVXgAdOLfQiLVdw7RRiNDDp9IQ+2FAGLTcyLfciJFIEPtgwDD69VwIkMh4tN
      5IkUgUDr0THSO1UcfBKLXdwDXQzHRbgAAAAAK13Q6yAxwDtFGH0Ni33kD7YcAQEc
      h0Dr7kIDTRjrz/9FuAN1GItF3CtF0AHwiceLRbg7RRx/LDHJO00YfeGLRQiLfcwB
      8A+2BAgrBI+LfeQDBI+ZiQSPjTwz933YiAQPQevWi0UIK0Xci03AAfCJRbiLXRCJ
      /itdHCt13AN14DnZfAgDdQwrdeDrSot1DDHbK3XcAf4DdeA7XRh9KItV4ItFuAHQ
      A1UID7YEGA+2FBop0ItV5AMEmokEmpn3fdiIBB5D69OLRRhBAUXg66OLRRhDAUXg
      O10QfTIxyTtNGH3ti33Ii0XgA0UID7YUCIsEjynQi1XkAwSKiQSKi1XgjTwWmfd9
      2IgED0Hr0ItF1P9FvAFF3AFF0OmE/v//i0Wkx0XcAAAAAMdFvAAAAACJRdCLRbAD
      RQyJRaCLRbw7RRAPjXABAACLTdwxwItdoIt10IlN4AHLi30Mi1XcO0UYjQw6fSEP
      thQBi33MD7YMA4kUh4t9yA+vVcCJDIeLTeSJFIFA69Ex0jtVHHwSi13cA10Ix0W4
      AAAAACtd0OsgMcA7RRh9DYt95A+2HAEBHIdA6+5CA03U68//RbgDddSLRdwrRdAB
      8InHi0W4O0UcfywxyTtNGH3hi0UMi33MAfAPtgQIKwSPi33kAwSPmYkEj408M/d9
      2IgED0Hr1otFDCtF3ItNwAHwiUW4i10Uif4rXRwrddwDdeA52XwIA3UIK3Xg60qL
      dQgx2yt13AH+A3XgO10YfSiLVeCLRbgB0ANVDA+2BBgPthQaKdCLVeQDBJqJBJqZ
      933YiAQeQ+vTi0XUQQFF4Ouji0XUQwFF4DtdFH0yMck7TRh97Yt9yItF4ANFDA+2
      FAiLBI+LfeQp0ItV4AMEj4kEj408Fpn3fdiIBA9B69CLRRj/RbwBRdwBRdDphP7/
      //9NrItltA+Fofz//9no3+l2PzHJMds7XRR9OotFGIt9CA+vwY1EBwMx/zt9EH0c
      D7Yw2cBHVtoMJFrZXeTzDyx15InyiBADRRjr30MDTRDrxd3Y6wLd2I1l9DHAW15f
      XcM=
      )"
			static x64 := "
      (LTrim
      VUFXQVZBVUFUV1ZTSIHsqAAAAEiNrCSAAAAARIutkAAAAIuFmAAAAESJxkiJVRhB
      jVH/SYnPi42YAAAARInHQQ+v9Y1EAAErvZgAAABEiUUARIlN2IlFFEljxcdFtAMA
      AABIY96LtZgAAABIiUUID6/TiV0ESIld4A+vy4udmAAAAIl9qPMPEI2gAAAAiVXQ
      SI0UhRIAAABBD6/1/8OJTbBIiVXoSINl6PCJXdxBifaJdbxBjXD/SWPGQQ+v9UiJ
      RZhIY8FIiUWQiXW4RInOK7WYAAAAiXWMSItF6EiJZcDoAAAAAEgpxEiLRehIieHo
      AAAAAEgpxEiLRehIiWX46AAAAABIKcRIi0UYTYn6SIll8MdFEAAAAADHRdQAAAAA
      SIlFyItF2DlF1A+NqgEAAESLTRAxwEWJyEQDTbhNY8lNAflBOcV+JUEPthQCSIt9
      +EUPthwBSItd8IkUhw+vVdxEiRyDiRSBSP/A69aLVRBFMclEO42YAAAAfA9Ii0WY
      RTHbMdtNjSQC6ytMY9oxwE0B+0E5xX4NQQ+2HAMBHIFI/8Dr7kH/wUQB6uvGTANd
      CP/DRQHoO52YAAAAi0W8Ro00AH82SItFyEuNPCNFMclJjTQDRTnNftRIi1X4Qg+2
      BA9CKwSKQgMEiZlCiQSJ930UQogEDkn/wevZi0UQSWP4SAN9GItd3E1j9kUx200B
      /kQpwIlFrEiJfaCLdaiLRaxEAcA580GJ8XwRSGP4TWPAMdtMAf9MA0UY60tIi0Wg
      S408Hk+NJBNFMclKjTQYRTnNfiFDD7YUDEIPtgQPKdBCAwSJmUKJBIn3fRRCiAQO
      Sf/B69r/w0UB6EwDXQjrm0gDXQhB/8FEO00AfTRMjSQfSY00GEUx20U53X7jSItF
      8EMPthQcQosEmCnQQgMEmZlCiQSZ930UQogEHkn/w+vXi0UEAUUQSItF4P9F1EgB
      RchJAcLpSv7//0yLVRhMiX3Ix0UQAAAAAMdF1AAAAACLRQA5RdQPja0BAABEi00Q
      McBFichEA03QTWPJTANNGEE5xX4lQQ+2FAJIi3X4RQ+2HAFIi33wiRSGD69V3ESJ
      HIeJFIFI/8Dr1otVEEUxyUQ7jZgAAAB8D0iLRZBFMdsx202NJALrLUxj2kwDXRgx
      wEE5xX4NQQ+2HAMBHIFI/8Dr7kH/wQNVBOvFRANFBEwDXeD/wzudmAAAAItFsEaN
      NAB/NkiLRchLjTwjRTHJSY00A0U5zX7TSItV+EIPtgQPQisEikIDBImZQokEifd9
      FEKIBA5J/8Hr2YtFEE1j9klj+EwDdRiLXdxFMdtEKcCJRaxJjQQ/SIlFoIt1jItF
      rEQBwDnzQYnxfBFNY8BIY/gx20gDfRhNAfjrTEiLRaBLjTweT40kE0UxyUqNNBhF
      Oc1+IUMPthQMQg+2BA8p0EIDBImZQokEifd9FEKIBA5J/8Hr2v/DRANFBEwDXeDr
      mkgDXeBB/8FEO03YfTRMjSQfSY00GEUx20U53X7jSItF8EMPthQcQosEmCnQQgME
      mZlCiQSZ930UQogEHkn/w+vXSItFCP9F1EQBbRBIAUXISQHC6Uf+////TbRIi2XA
      D4Ui/P//8w8QBQAAAAAPLsF2TTHJRTHARDtF2H1Cicgx0kEPr8VImEgrRQhNjQwH
      McBIA0UIO1UAfR1FD7ZUAQP/wvNBDyrC8w9ZwfNEDyzQRYhUAQPr2kH/wANNAOu4
      McBIjWUoW15fQVxBXUFeQV9dw5CQkJCQkJCQkJCQkJAAAIA/
      )"
			width := Gdip_GetImageWidth(pBitmap)
			height := Gdip_GetImageHeight(pBitmap)
			clone := Gdip_CloneBitmapArea(pBitmap, 0, 0, width, height)
			E1 := Gdip_LockBits(pBitmap, 0, 0, width, height, Stride1, Scan01, BitmapData1)
			E2 := Gdip_LockBits(clone, 0, 0, width, height, Stride2, Scan02, BitmapData2)
			
			DllCall("crypt32\CryptStringToBinary", "str",(A_PtrSize == 8) ? x64 : x86, "uint",0, "uint",0x1, "ptr",0, "uint*",s, "ptr",0, "ptr",0)
			p := DllCall("GlobalAlloc", "uint",0, "ptr",s, "ptr")
			if (A_PtrSize == 8)
				DllCall("VirtualProtect", "ptr",p, "ptr",s, "uint",0x40, "uint*",op)
			DllCall("crypt32\CryptStringToBinary", "str",(A_PtrSize == 8) ? x64 : x86, "uint",0, "uint",0x1, "ptr",p, "uint*",s, "ptr",0, "ptr",0)
			value := DllCall(p, "ptr",Scan01, "ptr",Scan02, "uint",width, "uint",height, "uint",4, "uint",radius, "float",opacity)
			DllCall("GlobalFree", "ptr", p)
			
			Gdip_UnlockBits(pBitmap, BitmapData1)
			Gdip_UnlockBits(clone, BitmapData2)
			Gdip_DisposeImage(clone)
			return value
		}
		
		outer[]
		{
			get {
         ; Determine if there is a parent class. this.__class will retrive the
         ; current instance's class name. Array notation [] will dereference.
         ; Returns void if this function is not nested in at least 2 classes.
				if ((_class := RegExReplace(this.__class, "^(.*)\..*$", "$1")) != this.__class)
					Loop, Parse, _class, .
						outer := (A_Index=1) ? %A_LoopField% : outer[A_LoopField]
				return outer
			}
		}
		
		x1() {
			return this.x
		}
		
		y1() {
			return this.y
		}
		
		x2() {
			return this.xx
		}
		
		y2() {
			return this.yy
		}
		
		width() {
			return this.xx - this.x
		}
		
		height() {
			return this.yy - this.y
		}
	} ; End of Subtitle class.
	
}

; Gdip standard library v1.45 by tic (Tariq Porter) 07/09/11


; Modifed by Rseding91 using fincs 64 bit compatible Gdip library 5/1/2013
; Supports: Basic, _L ANSi, _L Unicode x86 and _L Unicode x64
;
; Updated 2/20/2014 - fixed Gdip_CreateRegion() and Gdip_GetClipRegion() on AHK Unicode x86
; Updated 5/13/2013 - fixed Gdip_SetBitmapToClipboard() on AHK Unicode x64
;
;#####################################################################################
;#####################################################################################
; STATUS ENUMERATION
; Return values for functions specified to have status enumerated return type
;#####################################################################################
;
; Ok =						= 0
; GenericError				= 1
; InvalidParameter			= 2
; OutOfMemory				= 3
; ObjectBusy				= 4
; InsufficientBuffer		= 5
; NotImplemented			= 6
; Win32Error				= 7
; WrongState				= 8
; Aborted					= 9
; FileNotFound				= 10
; ValueOverflow				= 11
; AccessDenied				= 12
; UnknownImageFormat		= 13
; FontFamilyNotFound		= 14
; FontStyleNotFound			= 15
; NotTrueTypeFont			= 16
; UnsupportedGdiplusVersion	= 17
; GdiplusNotInitialized		= 18
; PropertyNotFound			= 19
; PropertyNotSupported		= 20
; ProfileNotFound			= 21
;
;#####################################################################################
;#####################################################################################
; FUNCTIONS
;#####################################################################################
;
; UpdateLayeredWindow(hwnd, hdc, x="", y="", w="", h="", Alpha=255)
; BitBlt(ddc, dx, dy, dw, dh, sdc, sx, sy, Raster="")
; StretchBlt(dDC, dx, dy, dw, dh, sDC, sx, sy, sw, sh, Raster="")
; SetImage(hwnd, hBitmap)
; Gdip_BitmapFromScreen(Screen=0, Raster="")
; CreateRectF(ByRef RectF, x, y, w, h)
; CreateSizeF(ByRef SizeF, w, h)
; CreateDIBSection
;
;#####################################################################################

; Function:     			UpdateLayeredWindow
; Description:  			Updates a layered window with the handle to the DC of a gdi bitmap
; 
; hwnd        				Handle of the layered window to update
; hdc           			Handle to the DC of the GDI bitmap to update the window with
; Layeredx      			x position to place the window
; Layeredy      			y position to place the window
; Layeredw      			Width of the window
; Layeredh      			Height of the window
; Alpha         			Default = 255 : The transparency (0-255) to set the window transparency
;
; return      				If the function succeeds, the return value is nonzero
;
; notes						If x or y omitted, then layered window will use its current coordinates
;							If w or h omitted then current width and height will be used

UpdateLayeredWindow(hwnd, hdc, x="", y="", w="", h="", Alpha=255)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if ((x != "") && (y != ""))
		VarSetCapacity(pt, 8), NumPut(x, pt, 0, "UInt"), NumPut(y, pt, 4, "UInt")
	
	if (w = "") ||(h = "")
		WinGetPos,,, w, h, ahk_id %hwnd%
	
	return DllCall("UpdateLayeredWindow"
					, Ptr, hwnd
					, Ptr, 0
					, Ptr, ((x = "") && (y = "")) ? 0 : &pt
					, "int64*", w|h<<32
					, Ptr, hdc
					, "int64*", 0
					, "uint", 0
					, "UInt*", Alpha<<16|1<<24
					, "uint", 2)
}

;#####################################################################################

; Function				BitBlt
; Description			The BitBlt function performs a bit-block transfer of the color data corresponding to a rectangle 
;						of pixels from the specified source device context into a destination device context.
;
; dDC					handle to destination DC
; dx					x-coord of destination upper-left corner
; dy					y-coord of destination upper-left corner
; dw					width of the area to copy
; dh					height of the area to copy
; sDC					handle to source DC
; sx					x-coordinate of source upper-left corner
; sy					y-coordinate of source upper-left corner
; Raster				raster operation code
;
; return				If the function succeeds, the return value is nonzero
;
; notes					If no raster operation is specified, then SRCCOPY is used, which copies the source directly to the destination rectangle
;
; BLACKNESS				= 0x00000042
; NOTSRCERASE			= 0x001100A6
; NOTSRCCOPY			= 0x00330008
; SRCERASE				= 0x00440328
; DSTINVERT				= 0x00550009
; PATINVERT				= 0x005A0049
; SRCINVERT				= 0x00660046
; SRCAND				= 0x008800C6
; MERGEPAINT			= 0x00BB0226
; MERGECOPY				= 0x00C000CA
; SRCCOPY				= 0x00CC0020
; SRCPAINT				= 0x00EE0086
; PATCOPY				= 0x00F00021
; PATPAINT				= 0x00FB0A09
; WHITENESS				= 0x00FF0062
; CAPTUREBLT			= 0x40000000
; NOMIRRORBITMAP		= 0x80000000

BitBlt(ddc, dx, dy, dw, dh, sdc, sx, sy, Raster="")
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdi32\BitBlt"
					, Ptr, dDC
					, "int", dx
					, "int", dy
					, "int", dw
					, "int", dh
					, Ptr, sDC
					, "int", sx
					, "int", sy
					, "uint", Raster ? Raster : 0x00CC0020)
}

;#####################################################################################

; Function				StretchBlt
; Description			The StretchBlt function copies a bitmap from a source rectangle into a destination rectangle, 
;						stretching or compressing the bitmap to fit the dimensions of the destination rectangle, if necessary.
;						The system stretches or compresses the bitmap according to the stretching mode currently set in the destination device context.
;
; ddc					handle to destination DC
; dx					x-coord of destination upper-left corner
; dy					y-coord of destination upper-left corner
; dw					width of destination rectangle
; dh					height of destination rectangle
; sdc					handle to source DC
; sx					x-coordinate of source upper-left corner
; sy					y-coordinate of source upper-left corner
; sw					width of source rectangle
; sh					height of source rectangle
; Raster				raster operation code
;
; return				If the function succeeds, the return value is nonzero
;
; notes					If no raster operation is specified, then SRCCOPY is used. It uses the same raster operations as BitBlt		

StretchBlt(ddc, dx, dy, dw, dh, sdc, sx, sy, sw, sh, Raster="")
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdi32\StretchBlt"
					, Ptr, ddc
					, "int", dx
					, "int", dy
					, "int", dw
					, "int", dh
					, Ptr, sdc
					, "int", sx
					, "int", sy
					, "int", sw
					, "int", sh
					, "uint", Raster ? Raster : 0x00CC0020)
}

;#####################################################################################

; Function				SetStretchBltMode
; Description			The SetStretchBltMode function sets the bitmap stretching mode in the specified device context
;
; hdc					handle to the DC
; iStretchMode			The stretching mode, describing how the target will be stretched
;
; return				If the function succeeds, the return value is the previous stretching mode. If it fails it will return 0
;
; STRETCH_ANDSCANS 		= 0x01
; STRETCH_ORSCANS 		= 0x02
; STRETCH_DELETESCANS 	= 0x03
; STRETCH_HALFTONE 		= 0x04

SetStretchBltMode(hdc, iStretchMode=4)
{
	return DllCall("gdi32\SetStretchBltMode"
					, A_PtrSize ? "UPtr" : "UInt", hdc
					, "int", iStretchMode)
}

;#####################################################################################

; Function				SetImage
; Description			Associates a new image with a static control
;
; hwnd					handle of the control to update
; hBitmap				a gdi bitmap to associate the static control with
;
; return				If the function succeeds, the return value is nonzero

SetImage(hwnd, hBitmap)
{
	SendMessage, 0x172, 0x0, hBitmap,, ahk_id %hwnd%
	E := ErrorLevel
	DeleteObject(E)
	return E
}

;#####################################################################################

; Function				SetSysColorToControl
; Description			Sets a solid colour to a control
;
; hwnd					handle of the control to update
; SysColor				A system colour to set to the control
;
; return				If the function succeeds, the return value is zero
;
; notes					A control must have the 0xE style set to it so it is recognised as a bitmap
;						By default SysColor=15 is used which is COLOR_3DFACE. This is the standard background for a control
;
; COLOR_3DDKSHADOW				= 21
; COLOR_3DFACE					= 15
; COLOR_3DHIGHLIGHT				= 20
; COLOR_3DHILIGHT				= 20
; COLOR_3DLIGHT					= 22
; COLOR_3DSHADOW				= 16
; COLOR_ACTIVEBORDER			= 10
; COLOR_ACTIVECAPTION			= 2
; COLOR_APPWORKSPACE			= 12
; COLOR_BACKGROUND				= 1
; COLOR_BTNFACE					= 15
; COLOR_BTNHIGHLIGHT			= 20
; COLOR_BTNHILIGHT				= 20
; COLOR_BTNSHADOW				= 16
; COLOR_BTNTEXT					= 18
; COLOR_CAPTIONTEXT				= 9
; COLOR_DESKTOP					= 1
; COLOR_GRADIENTACTIVECAPTION	= 27
; COLOR_GRADIENTINACTIVECAPTION	= 28
; COLOR_GRAYTEXT				= 17
; COLOR_HIGHLIGHT				= 13
; COLOR_HIGHLIGHTTEXT			= 14
; COLOR_HOTLIGHT				= 26
; COLOR_INACTIVEBORDER			= 11
; COLOR_INACTIVECAPTION			= 3
; COLOR_INACTIVECAPTIONTEXT		= 19
; COLOR_INFOBK					= 24
; COLOR_INFOTEXT				= 23
; COLOR_MENU					= 4
; COLOR_MENUHILIGHT				= 29
; COLOR_MENUBAR					= 30
; COLOR_MENUTEXT				= 7
; COLOR_SCROLLBAR				= 0
; COLOR_WINDOW					= 5
; COLOR_WINDOWFRAME				= 6
; COLOR_WINDOWTEXT				= 8

SetSysColorToControl(hwnd, SysColor=15)
{
	WinGetPos,,, w, h, ahk_id %hwnd%
	bc := DllCall("GetSysColor", "Int", SysColor, "UInt")
	pBrushClear := Gdip_BrushCreateSolid(0xff000000 | (bc >> 16 | bc & 0xff00 | (bc & 0xff) << 16))
	pBitmap := Gdip_CreateBitmap(w, h), G := Gdip_GraphicsFromImage(pBitmap)
	Gdip_FillRectangle(G, pBrushClear, 0, 0, w, h)
	hBitmap := Gdip_CreateHBITMAPFromBitmap(pBitmap)
	SetImage(hwnd, hBitmap)
	Gdip_DeleteBrush(pBrushClear)
	Gdip_DeleteGraphics(G), Gdip_DisposeImage(pBitmap), DeleteObject(hBitmap)
	return 0
}

;#####################################################################################

; Function				Gdip_BitmapFromScreen
; Description			Gets a gdi+ bitmap from the screen
;
; Screen				0 = All screens
;						Any numerical value = Just that screen
;						x|y|w|h = Take specific coordinates with a width and height
; Raster				raster operation code
;
; return      			If the function succeeds, the return value is a pointer to a gdi+ bitmap
;						-1:		one or more of x,y,w,h not passed properly
;
; notes					If no raster operation is specified, then SRCCOPY is used to the returned bitmap

Gdip_BitmapFromScreen(Screen=0, Raster="")
{
	if (Screen = 0)
	{
		Sysget, x, 76
		Sysget, y, 77	
		Sysget, w, 78
		Sysget, h, 79
	}
	else if (SubStr(Screen, 1, 5) = "hwnd:")
	{
		Screen := SubStr(Screen, 6)
		if !WinExist( "ahk_id " Screen)
			return -2
		WinGetPos,,, w, h, ahk_id %Screen%
		x := y := 0
		hhdc := GetDCEx(Screen, 3)
	}
	else if (Screen&1 != "")
	{
		Sysget, M, Monitor, %Screen%
		x := MLeft, y := MTop, w := MRight-MLeft, h := MBottom-MTop
	}
	else
	{
		StringSplit, S, Screen, |
		x := S1, y := S2, w := S3, h := S4
	}
	
	if (x = "") || (y = "") || (w = "") || (h = "")
		return -1
	
	chdc := CreateCompatibleDC(), hbm := CreateDIBSection(w, h, chdc), obm := SelectObject(chdc, hbm), hhdc := hhdc ? hhdc : GetDC()
	BitBlt(chdc, 0, 0, w, h, hhdc, x, y, Raster)
	ReleaseDC(hhdc)
	
	pBitmap := Gdip_CreateBitmapFromHBITMAP(hbm)
	SelectObject(chdc, obm), DeleteObject(hbm), DeleteDC(hhdc), DeleteDC(chdc)
	return pBitmap
}

;#####################################################################################

; Function				Gdip_BitmapFromHWND
; Description			Uses PrintWindow to get a handle to the specified window and return a bitmap from it
;
; hwnd					handle to the window to get a bitmap from
;
; return				If the function succeeds, the return value is a pointer to a gdi+ bitmap
;
; notes					Window must not be not minimised in order to get a handle to it's client area

Gdip_BitmapFromHWND(hwnd)
{
	WinGetPos,,, Width, Height, ahk_id %hwnd%
	hbm := CreateDIBSection(Width, Height), hdc := CreateCompatibleDC(), obm := SelectObject(hdc, hbm)
	PrintWindow(hwnd, hdc)
	pBitmap := Gdip_CreateBitmapFromHBITMAP(hbm)
	SelectObject(hdc, obm), DeleteObject(hbm), DeleteDC(hdc)
	return pBitmap
}

;#####################################################################################

; Function    			CreateRectF
; Description			Creates a RectF object, containing a the coordinates and dimensions of a rectangle
;
; RectF       			Name to call the RectF object
; x            			x-coordinate of the upper left corner of the rectangle
; y            			y-coordinate of the upper left corner of the rectangle
; w            			Width of the rectangle
; h            			Height of the rectangle
;
; return      			No return value

CreateRectF(ByRef RectF, x, y, w, h)
{
	VarSetCapacity(RectF, 16)
	NumPut(x, RectF, 0, "float"), NumPut(y, RectF, 4, "float"), NumPut(w, RectF, 8, "float"), NumPut(h, RectF, 12, "float")
}

;#####################################################################################

; Function    			CreateRect
; Description			Creates a Rect object, containing a the coordinates and dimensions of a rectangle
;
; RectF       			Name to call the RectF object
; x            			x-coordinate of the upper left corner of the rectangle
; y            			y-coordinate of the upper left corner of the rectangle
; w            			Width of the rectangle
; h            			Height of the rectangle
;
; return      			No return value

CreateRect(ByRef Rect, x, y, w, h)
{
	VarSetCapacity(Rect, 16)
	NumPut(x, Rect, 0, "uint"), NumPut(y, Rect, 4, "uint"), NumPut(w, Rect, 8, "uint"), NumPut(h, Rect, 12, "uint")
}
;#####################################################################################

; Function		    	CreateSizeF
; Description			Creates a SizeF object, containing an 2 values
;
; SizeF         		Name to call the SizeF object
; w            			w-value for the SizeF object
; h            			h-value for the SizeF object
;
; return      			No Return value

CreateSizeF(ByRef SizeF, w, h)
{
	VarSetCapacity(SizeF, 8)
	NumPut(w, SizeF, 0, "float"), NumPut(h, SizeF, 4, "float")     
}
;#####################################################################################

; Function		    	CreatePointF
; Description			Creates a SizeF object, containing an 2 values
;
; SizeF         		Name to call the SizeF object
; w            			w-value for the SizeF object
; h            			h-value for the SizeF object
;
; return      			No Return value

CreatePointF(ByRef PointF, x, y)
{
	VarSetCapacity(PointF, 8)
	NumPut(x, PointF, 0, "float"), NumPut(y, PointF, 4, "float")     
}
;#####################################################################################

; Function				CreateDIBSection
; Description			The CreateDIBSection function creates a DIB (Device Independent Bitmap) that applications can write to directly
;
; w						width of the bitmap to create
; h						height of the bitmap to create
; hdc					a handle to the device context to use the palette from
; bpp					bits per pixel (32 = ARGB)
; ppvBits				A pointer to a variable that receives a pointer to the location of the DIB bit values
;
; return				returns a DIB. A gdi bitmap
;
; notes					ppvBits will receive the location of the pixels in the DIB

CreateDIBSection(w, h, hdc="", bpp=32, ByRef ppvBits=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	hdc2 := hdc ? hdc : GetDC()
	VarSetCapacity(bi, 40, 0)
	
	NumPut(w, bi, 4, "uint")
	, NumPut(h, bi, 8, "uint")
	, NumPut(40, bi, 0, "uint")
	, NumPut(1, bi, 12, "ushort")
	, NumPut(0, bi, 16, "uInt")
	, NumPut(bpp, bi, 14, "ushort")
	
	hbm := DllCall("CreateDIBSection"
					, Ptr, hdc2
					, Ptr, &bi
					, "uint", 0
					, A_PtrSize ? "UPtr*" : "uint*", ppvBits
					, Ptr, 0
					, "uint", 0, Ptr)
	
	if !hdc
		ReleaseDC(hdc2)
	return hbm
}

;#####################################################################################

; Function				PrintWindow
; Description			The PrintWindow function copies a visual window into the specified device context (DC), typically a printer DC
;
; hwnd					A handle to the window that will be copied
; hdc					A handle to the device context
; Flags					Drawing options
;
; return				If the function succeeds, it returns a nonzero value
;
; PW_CLIENTONLY			= 1

PrintWindow(hwnd, hdc, Flags=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("PrintWindow", Ptr, hwnd, Ptr, hdc, "uint", Flags)
}

;#####################################################################################

; Function				DestroyIcon
; Description			Destroys an icon and frees any memory the icon occupied
;
; hIcon					Handle to the icon to be destroyed. The icon must not be in use
;
; return				If the function succeeds, the return value is nonzero

DestroyIcon(hIcon)
{
	return DllCall("DestroyIcon", A_PtrSize ? "UPtr" : "UInt", hIcon)
}

;#####################################################################################

PaintDesktop(hdc)
{
	return DllCall("PaintDesktop", A_PtrSize ? "UPtr" : "UInt", hdc)
}

;#####################################################################################

CreateCompatibleBitmap(hdc, w, h)
{
	return DllCall("gdi32\CreateCompatibleBitmap", A_PtrSize ? "UPtr" : "UInt", hdc, "int", w, "int", h)
}

;#####################################################################################

; Function				CreateCompatibleDC
; Description			This function creates a memory device context (DC) compatible with the specified device
;
; hdc					Handle to an existing device context					
;
; return				returns the handle to a device context or 0 on failure
;
; notes					If this handle is 0 (by default), the function creates a memory device context compatible with the application's current screen

CreateCompatibleDC(hdc=0)
{
	return DllCall("CreateCompatibleDC", A_PtrSize ? "UPtr" : "UInt", hdc)
}

;#####################################################################################

; Function				SelectObject
; Description			The SelectObject function selects an object into the specified device context (DC). The new object replaces the previous object of the same type
;
; hdc					Handle to a DC
; hgdiobj				A handle to the object to be selected into the DC
;
; return				If the selected object is not a region and the function succeeds, the return value is a handle to the object being replaced
;
; notes					The specified object must have been created by using one of the following functions
;						Bitmap - CreateBitmap, CreateBitmapIndirect, CreateCompatibleBitmap, CreateDIBitmap, CreateDIBSection (A single bitmap cannot be selected into more than one DC at the same time)
;						Brush - CreateBrushIndirect, CreateDIBPatternBrush, CreateDIBPatternBrushPt, CreateHatchBrush, CreatePatternBrush, CreateSolidBrush
;						Font - CreateFont, CreateFontIndirect
;						Pen - CreatePen, CreatePenIndirect
;						Region - CombineRgn, CreateEllipticRgn, CreateEllipticRgnIndirect, CreatePolygonRgn, CreateRectRgn, CreateRectRgnIndirect
;
; notes					If the selected object is a region and the function succeeds, the return value is one of the following value
;
; SIMPLEREGION			= 2 Region consists of a single rectangle
; COMPLEXREGION			= 3 Region consists of more than one rectangle
; NULLREGION			= 1 Region is empty

SelectObject(hdc, hgdiobj)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("SelectObject", Ptr, hdc, Ptr, hgdiobj)
}

;#####################################################################################

; Function				DeleteObject
; Description			This function deletes a logical pen, brush, font, bitmap, region, or palette, freeing all system resources associated with the object
;						After the object is deleted, the specified handle is no longer valid
;
; hObject				Handle to a logical pen, brush, font, bitmap, region, or palette to delete
;
; return				Nonzero indicates success. Zero indicates that the specified handle is not valid or that the handle is currently selected into a device context

DeleteObject(hObject)
{
	return DllCall("DeleteObject", A_PtrSize ? "UPtr" : "UInt", hObject)
}

;#####################################################################################

; Function				GetDC
; Description			This function retrieves a handle to a display device context (DC) for the client area of the specified window.
;						The display device context can be used in subsequent graphics display interface (GDI) functions to draw in the client area of the window. 
;
; hwnd					Handle to the window whose device context is to be retrieved. If this value is NULL, GetDC retrieves the device context for the entire screen					
;
; return				The handle the device context for the specified window's client area indicates success. NULL indicates failure

GetDC(hwnd=0)
{
	return DllCall("GetDC", A_PtrSize ? "UPtr" : "UInt", hwnd)
}

;#####################################################################################

; DCX_CACHE = 0x2
; DCX_CLIPCHILDREN = 0x8
; DCX_CLIPSIBLINGS = 0x10
; DCX_EXCLUDERGN = 0x40
; DCX_EXCLUDEUPDATE = 0x100
; DCX_INTERSECTRGN = 0x80
; DCX_INTERSECTUPDATE = 0x200
; DCX_LOCKWINDOWUPDATE = 0x400
; DCX_NORECOMPUTE = 0x100000
; DCX_NORESETATTRS = 0x4
; DCX_PARENTCLIP = 0x20
; DCX_VALIDATE = 0x200000
; DCX_WINDOW = 0x1

GetDCEx(hwnd, flags=0, hrgnClip=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("GetDCEx", Ptr, hwnd, Ptr, hrgnClip, "int", flags)
}

;#####################################################################################

; Function				ReleaseDC
; Description			This function releases a device context (DC), freeing it for use by other applications. The effect of ReleaseDC depends on the type of device context
;
; hdc					Handle to the device context to be released
; hwnd					Handle to the window whose device context is to be released
;
; return				1 = released
;						0 = not released
;
; notes					The application must call the ReleaseDC function for each call to the GetWindowDC function and for each call to the GetDC function that retrieves a common device context
;						An application cannot use the ReleaseDC function to release a device context that was created by calling the CreateDC function; instead, it must use the DeleteDC function. 

ReleaseDC(hdc, hwnd=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("ReleaseDC", Ptr, hwnd, Ptr, hdc)
}

;#####################################################################################

; Function				DeleteDC
; Description			The DeleteDC function deletes the specified device context (DC)
;
; hdc					A handle to the device context
;
; return				If the function succeeds, the return value is nonzero
;
; notes					An application must not delete a DC whose handle was obtained by calling the GetDC function. Instead, it must call the ReleaseDC function to free the DC

DeleteDC(hdc)
{
	return DllCall("DeleteDC", A_PtrSize ? "UPtr" : "UInt", hdc)
}
;#####################################################################################

; Function				Gdip_LibraryVersion
; Description			Get the current library version
;
; return				the library version
;
; notes					This is useful for non compiled programs to ensure that a person doesn't run an old version when testing your scripts

Gdip_LibraryVersion()
{
	return 1.45
}

;#####################################################################################

; Function				Gdip_LibrarySubVersion
; Description			Get the current library sub version
;
; return				the library sub version
;
; notes					This is the sub-version currently maintained by Rseding91
Gdip_LibrarySubVersion()
{
	return 1.47
}

;#####################################################################################

; Function:    			Gdip_BitmapFromBRA
; Description: 			Gets a pointer to a gdi+ bitmap from a BRA file
;
; BRAFromMemIn			The variable for a BRA file read to memory
; File					The name of the file, or its number that you would like (This depends on alternate parameter)
; Alternate				Changes whether the File parameter is the file name or its number
;
; return      			If the function succeeds, the return value is a pointer to a gdi+ bitmap
;						-1 = The BRA variable is empty
;						-2 = The BRA has an incorrect header
;						-3 = The BRA has information missing
;						-4 = Could not find file inside the BRA

Gdip_BitmapFromBRA(ByRef BRAFromMemIn, File, Alternate=0)
{
	Static FName = "ObjRelease"
	
	if !BRAFromMemIn
		return -1
	Loop, Parse, BRAFromMemIn, `n
	{
		if (A_Index = 1)
		{
			StringSplit, Header, A_LoopField, |
			if (Header0 != 4 || Header2 != "BRA!")
				return -2
		}
		else if (A_Index = 2)
		{
			StringSplit, Info, A_LoopField, |
			if (Info0 != 3)
				return -3
		}
		else
			break
	}
	if !Alternate
		StringReplace, File, File, \, \\, All
	RegExMatch(BRAFromMemIn, "mi`n)^" (Alternate ? File "\|.+?\|(\d+)\|(\d+)" : "\d+\|" File "\|(\d+)\|(\d+)") "$", FileInfo)
	if !FileInfo
		return -4
	
	hData := DllCall("GlobalAlloc", "uint", 2, Ptr, FileInfo2, Ptr)
	pData := DllCall("GlobalLock", Ptr, hData, Ptr)
	DllCall("RtlMoveMemory", Ptr, pData, Ptr, &BRAFromMemIn+Info2+FileInfo1, Ptr, FileInfo2)
	DllCall("GlobalUnlock", Ptr, hData)
	DllCall("ole32\CreateStreamOnHGlobal", Ptr, hData, "int", 1, A_PtrSize ? "UPtr*" : "UInt*", pStream)
	DllCall("gdiplus\GdipCreateBitmapFromStream", Ptr, pStream, A_PtrSize ? "UPtr*" : "UInt*", pBitmap)
	If (A_PtrSize)
		%FName%(pStream)
	Else
		DllCall(NumGet(NumGet(1*pStream)+8), "uint", pStream)
	return pBitmap
}

;#####################################################################################

; Function				Gdip_DrawRectangle
; Description			This function uses a pen to draw the outline of a rectangle into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x						x-coordinate of the top left of the rectangle
; y						y-coordinate of the top left of the rectangle
; w						width of the rectanlge
; h						height of the rectangle
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawRectangle(pGraphics, pPen, x, y, w, h)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawRectangle", Ptr, pGraphics, Ptr, pPen, "float", x, "float", y, "float", w, "float", h)
}

;#####################################################################################

; Function				Gdip_DrawRoundedRectangle
; Description			This function uses a pen to draw the outline of a rounded rectangle into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x						x-coordinate of the top left of the rounded rectangle
; y						y-coordinate of the top left of the rounded rectangle
; w						width of the rectanlge
; h						height of the rectangle
; r						radius of the rounded corners
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawRoundedRectangle(pGraphics, pPen, x, y, w, h, r)
{
	Gdip_SetClipRect(pGraphics, x-r, y-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x+w-r, y-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x-r, y+h-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x+w-r, y+h-r, 2*r, 2*r, 4)
	E := Gdip_DrawRectangle(pGraphics, pPen, x, y, w, h)
	Gdip_ResetClip(pGraphics)
	Gdip_SetClipRect(pGraphics, x-(2*r), y+r, w+(4*r), h-(2*r), 4)
	Gdip_SetClipRect(pGraphics, x+r, y-(2*r), w-(2*r), h+(4*r), 4)
	Gdip_DrawEllipse(pGraphics, pPen, x, y, 2*r, 2*r)
	Gdip_DrawEllipse(pGraphics, pPen, x+w-(2*r), y, 2*r, 2*r)
	Gdip_DrawEllipse(pGraphics, pPen, x, y+h-(2*r), 2*r, 2*r)
	Gdip_DrawEllipse(pGraphics, pPen, x+w-(2*r), y+h-(2*r), 2*r, 2*r)
	Gdip_ResetClip(pGraphics)
	return E
}

;#####################################################################################

; Function				Gdip_DrawEllipse
; Description			This function uses a pen to draw the outline of an ellipse into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x						x-coordinate of the top left of the rectangle the ellipse will be drawn into
; y						y-coordinate of the top left of the rectangle the ellipse will be drawn into
; w						width of the ellipse
; h						height of the ellipse
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawEllipse(pGraphics, pPen, x, y, w, h)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawEllipse", Ptr, pGraphics, Ptr, pPen, "float", x, "float", y, "float", w, "float", h)
}

;#####################################################################################

; Function				Gdip_DrawBezier
; Description			This function uses a pen to draw the outline of a bezier (a weighted curve) into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x1					x-coordinate of the start of the bezier
; y1					y-coordinate of the start of the bezier
; x2					x-coordinate of the first arc of the bezier
; y2					y-coordinate of the first arc of the bezier
; x3					x-coordinate of the second arc of the bezier
; y3					y-coordinate of the second arc of the bezier
; x4					x-coordinate of the end of the bezier
; y4					y-coordinate of the end of the bezier
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawBezier(pGraphics, pPen, x1, y1, x2, y2, x3, y3, x4, y4)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawBezier"
					, Ptr, pgraphics
					, Ptr, pPen
					, "float", x1
					, "float", y1
					, "float", x2
					, "float", y2
					, "float", x3
					, "float", y3
					, "float", x4
					, "float", y4)
}

;#####################################################################################

; Function				Gdip_DrawArc
; Description			This function uses a pen to draw the outline of an arc into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x						x-coordinate of the start of the arc
; y						y-coordinate of the start of the arc
; w						width of the arc
; h						height of the arc
; StartAngle			specifies the angle between the x-axis and the starting point of the arc
; SweepAngle			specifies the angle between the starting and ending points of the arc
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawArc(pGraphics, pPen, x, y, w, h, StartAngle, SweepAngle)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawArc"
					, Ptr, pGraphics
					, Ptr, pPen
					, "float", x
					, "float", y
					, "float", w
					, "float", h
					, "float", StartAngle
					, "float", SweepAngle)
}

;#####################################################################################

; Function				Gdip_DrawPie
; Description			This function uses a pen to draw the outline of a pie into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x						x-coordinate of the start of the pie
; y						y-coordinate of the start of the pie
; w						width of the pie
; h						height of the pie
; StartAngle			specifies the angle between the x-axis and the starting point of the pie
; SweepAngle			specifies the angle between the starting and ending points of the pie
;
; return				status enumeration. 0 = success
;
; notes					as all coordinates are taken from the top left of each pixel, then the entire width/height should be specified as subtracting the pen width

Gdip_DrawPie(pGraphics, pPen, x, y, w, h, StartAngle, SweepAngle)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawPie", Ptr, pGraphics, Ptr, pPen, "float", x, "float", y, "float", w, "float", h, "float", StartAngle, "float", SweepAngle)
}

;#####################################################################################

; Function				Gdip_DrawLine
; Description			This function uses a pen to draw a line into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; x1					x-coordinate of the start of the line
; y1					y-coordinate of the start of the line
; x2					x-coordinate of the end of the line
; y2					y-coordinate of the end of the line
;
; return				status enumeration. 0 = success		

Gdip_DrawLine(pGraphics, pPen, x1, y1, x2, y2)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipDrawLine"
					, Ptr, pGraphics
					, Ptr, pPen
					, "float", x1
					, "float", y1
					, "float", x2
					, "float", y2)
}

;#####################################################################################

; Function				Gdip_DrawLines
; Description			This function uses a pen to draw a series of joined lines into the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pPen					Pointer to a pen
; Points				the coordinates of all the points passed as x1,y1|x2,y2|x3,y3.....
;
; return				status enumeration. 0 = success				

Gdip_DrawLines(pGraphics, pPen, Points)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	StringSplit, Points, Points, |
	VarSetCapacity(PointF, 8*Points0)   
	Loop, %Points0%
	{
		StringSplit, Coord, Points%A_Index%, `,
		NumPut(Coord1, PointF, 8*(A_Index-1), "float"), NumPut(Coord2, PointF, (8*(A_Index-1))+4, "float")
	}
	return DllCall("gdiplus\GdipDrawLines", Ptr, pGraphics, Ptr, pPen, Ptr, &PointF, "int", Points0)
}

;#####################################################################################

; Function				Gdip_FillRectangle
; Description			This function uses a brush to fill a rectangle in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; x						x-coordinate of the top left of the rectangle
; y						y-coordinate of the top left of the rectangle
; w						width of the rectanlge
; h						height of the rectangle
;
; return				status enumeration. 0 = success

Gdip_FillRectangle(pGraphics, pBrush, x, y, w, h)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipFillRectangle"
					, Ptr, pGraphics
					, Ptr, pBrush
					, "float", x
					, "float", y
					, "float", w
					, "float", h)
}

;#####################################################################################

; Function				Gdip_FillRoundedRectangle
; Description			This function uses a brush to fill a rounded rectangle in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; x						x-coordinate of the top left of the rounded rectangle
; y						y-coordinate of the top left of the rounded rectangle
; w						width of the rectanlge
; h						height of the rectangle
; r						radius of the rounded corners
;
; return				status enumeration. 0 = success

Gdip_FillRoundedRectangle(pGraphics, pBrush, x, y, w, h, r)
{
	Region := Gdip_GetClipRegion(pGraphics)
	Gdip_SetClipRect(pGraphics, x-r, y-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x+w-r, y-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x-r, y+h-r, 2*r, 2*r, 4)
	Gdip_SetClipRect(pGraphics, x+w-r, y+h-r, 2*r, 2*r, 4)
	E := Gdip_FillRectangle(pGraphics, pBrush, x, y, w, h)
	Gdip_SetClipRegion(pGraphics, Region, 0)
	Gdip_SetClipRect(pGraphics, x-(2*r), y+r, w+(4*r), h-(2*r), 4)
	Gdip_SetClipRect(pGraphics, x+r, y-(2*r), w-(2*r), h+(4*r), 4)
	Gdip_FillEllipse(pGraphics, pBrush, x, y, 2*r, 2*r)
	Gdip_FillEllipse(pGraphics, pBrush, x+w-(2*r), y, 2*r, 2*r)
	Gdip_FillEllipse(pGraphics, pBrush, x, y+h-(2*r), 2*r, 2*r)
	Gdip_FillEllipse(pGraphics, pBrush, x+w-(2*r), y+h-(2*r), 2*r, 2*r)
	Gdip_SetClipRegion(pGraphics, Region, 0)
	Gdip_DeleteRegion(Region)
	return E
}

;#####################################################################################

; Function				Gdip_FillPolygon
; Description			This function uses a brush to fill a polygon in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; Points				the coordinates of all the points passed as x1,y1|x2,y2|x3,y3.....
;
; return				status enumeration. 0 = success
;
; notes					Alternate will fill the polygon as a whole, wheras winding will fill each new "segment"
; Alternate 			= 0
; Winding 				= 1

Gdip_FillPolygon(pGraphics, pBrush, Points, FillMode=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	StringSplit, Points, Points, |
	VarSetCapacity(PointF, 8*Points0)   
	Loop, %Points0%
	{
		StringSplit, Coord, Points%A_Index%, `,
		NumPut(Coord1, PointF, 8*(A_Index-1), "float"), NumPut(Coord2, PointF, (8*(A_Index-1))+4, "float")
	}   
	return DllCall("gdiplus\GdipFillPolygon", Ptr, pGraphics, Ptr, pBrush, Ptr, &PointF, "int", Points0, "int", FillMode)
}

;#####################################################################################

; Function				Gdip_FillPie
; Description			This function uses a brush to fill a pie in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; x						x-coordinate of the top left of the pie
; y						y-coordinate of the top left of the pie
; w						width of the pie
; h						height of the pie
; StartAngle			specifies the angle between the x-axis and the starting point of the pie
; SweepAngle			specifies the angle between the starting and ending points of the pie
;
; return				status enumeration. 0 = success

Gdip_FillPie(pGraphics, pBrush, x, y, w, h, StartAngle, SweepAngle)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipFillPie"
					, Ptr, pGraphics
					, Ptr, pBrush
					, "float", x
					, "float", y
					, "float", w
					, "float", h
					, "float", StartAngle
					, "float", SweepAngle)
}

;#####################################################################################

; Function				Gdip_FillEllipse
; Description			This function uses a brush to fill an ellipse in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; x						x-coordinate of the top left of the ellipse
; y						y-coordinate of the top left of the ellipse
; w						width of the ellipse
; h						height of the ellipse
;
; return				status enumeration. 0 = success

Gdip_FillEllipse(pGraphics, pBrush, x, y, w, h)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipFillEllipse", Ptr, pGraphics, Ptr, pBrush, "float", x, "float", y, "float", w, "float", h)
}

;#####################################################################################

; Function				Gdip_FillRegion
; Description			This function uses a brush to fill a region in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; Region				Pointer to a Region
;
; return				status enumeration. 0 = success
;
; notes					You can create a region Gdip_CreateRegion() and then add to this

Gdip_FillRegion(pGraphics, pBrush, Region)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipFillRegion", Ptr, pGraphics, Ptr, pBrush, Ptr, Region)
}

;#####################################################################################

; Function				Gdip_FillPath
; Description			This function uses a brush to fill a path in the Graphics of a bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBrush				Pointer to a brush
; Region				Pointer to a Path
;
; return				status enumeration. 0 = success

Gdip_FillPath(pGraphics, pBrush, Path)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipFillPath", Ptr, pGraphics, Ptr, pBrush, Ptr, Path)
}

;#####################################################################################

; Function				Gdip_DrawImagePointsRect
; Description			This function draws a bitmap into the Graphics of another bitmap and skews it
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBitmap				Pointer to a bitmap to be drawn
; Points				Points passed as x1,y1|x2,y2|x3,y3 (3 points: top left, top right, bottom left) describing the drawing of the bitmap
; sx					x-coordinate of source upper-left corner
; sy					y-coordinate of source upper-left corner
; sw					width of source rectangle
; sh					height of source rectangle
; Matrix				a matrix used to alter image attributes when drawing
;
; return				status enumeration. 0 = success
;
; notes					if sx,sy,sw,sh are missed then the entire source bitmap will be used
;						Matrix can be omitted to just draw with no alteration to ARGB
;						Matrix may be passed as a digit from 0 - 1 to change just transparency
;						Matrix can be passed as a matrix with any delimiter

Gdip_DrawImagePointsRect(pGraphics, pBitmap, Points, sx="", sy="", sw="", sh="", Matrix=1)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	StringSplit, Points, Points, |
	VarSetCapacity(PointF, 8*Points0)   
	Loop, %Points0%
	{
		StringSplit, Coord, Points%A_Index%, `,
		NumPut(Coord1, PointF, 8*(A_Index-1), "float"), NumPut(Coord2, PointF, (8*(A_Index-1))+4, "float")
	}
	
	if (Matrix&1 = "")
		ImageAttr := Gdip_SetImageAttributesColorMatrix(Matrix)
	else if (Matrix != 1)
		ImageAttr := Gdip_SetImageAttributesColorMatrix("1|0|0|0|0|0|1|0|0|0|0|0|1|0|0|0|0|0|" Matrix "|0|0|0|0|0|1")
	
	if (sx = "" && sy = "" && sw = "" && sh = "")
	{
		sx := 0, sy := 0
		sw := Gdip_GetImageWidth(pBitmap)
		sh := Gdip_GetImageHeight(pBitmap)
	}
	
	E := DllCall("gdiplus\GdipDrawImagePointsRect"
				, Ptr, pGraphics
				, Ptr, pBitmap
				, Ptr, &PointF
				, "int", Points0
				, "float", sx
				, "float", sy
				, "float", sw
				, "float", sh
				, "int", 2
				, Ptr, ImageAttr
				, Ptr, 0
				, Ptr, 0)
	if ImageAttr
		Gdip_DisposeImageAttributes(ImageAttr)
	return E
}

;#####################################################################################

; Function				Gdip_DrawImage
; Description			This function draws a bitmap into the Graphics of another bitmap
;
; pGraphics				Pointer to the Graphics of a bitmap
; pBitmap				Pointer to a bitmap to be drawn
; dx					x-coord of destination upper-left corner
; dy					y-coord of destination upper-left corner
; dw					width of destination image
; dh					height of destination image
; sx					x-coordinate of source upper-left corner
; sy					y-coordinate of source upper-left corner
; sw					width of source image
; sh					height of source image
; Matrix				a matrix used to alter image attributes when drawing
;
; return				status enumeration. 0 = success
;
; notes					if sx,sy,sw,sh are missed then the entire source bitmap will be used
;						Gdip_DrawImage performs faster
;						Matrix can be omitted to just draw with no alteration to ARGB
;						Matrix may be passed as a digit from 0 - 1 to change just transparency
;						Matrix can be passed as a matrix with any delimiter. For example:
;						MatrixBright=
;						(
;						1.5		|0		|0		|0		|0
;						0		|1.5	|0		|0		|0
;						0		|0		|1.5	|0		|0
;						0		|0		|0		|1		|0
;						0.05	|0.05	|0.05	|0		|1
;						)
;
; notes					MatrixBright = 1.5|0|0|0|0|0|1.5|0|0|0|0|0|1.5|0|0|0|0|0|1|0|0.05|0.05|0.05|0|1
;						MatrixGreyScale = 0.299|0.299|0.299|0|0|0.587|0.587|0.587|0|0|0.114|0.114|0.114|0|0|0|0|0|1|0|0|0|0|0|1
;						MatrixNegative = -1|0|0|0|0|0|-1|0|0|0|0|0|-1|0|0|0|0|0|1|0|0|0|0|0|1

Gdip_DrawImage(pGraphics, pBitmap, dx="", dy="", dw="", dh="", sx="", sy="", sw="", sh="", Matrix=1)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if (Matrix&1 = "")
		ImageAttr := Gdip_SetImageAttributesColorMatrix(Matrix)
	else if (Matrix != 1)
		ImageAttr := Gdip_SetImageAttributesColorMatrix("1|0|0|0|0|0|1|0|0|0|0|0|1|0|0|0|0|0|" Matrix "|0|0|0|0|0|1")
	
	if (sx = "" && sy = "" && sw = "" && sh = "")
	{
		if (dx = "" && dy = "" && dw = "" && dh = "")
		{
			sx := dx := 0, sy := dy := 0
			sw := dw := Gdip_GetImageWidth(pBitmap)
			sh := dh := Gdip_GetImageHeight(pBitmap)
		}
		else
		{
			sx := sy := 0
			sw := Gdip_GetImageWidth(pBitmap)
			sh := Gdip_GetImageHeight(pBitmap)
		}
	}
	
	E := DllCall("gdiplus\GdipDrawImageRectRect"
				, Ptr, pGraphics
				, Ptr, pBitmap
				, "float", dx
				, "float", dy
				, "float", dw
				, "float", dh
				, "float", sx
				, "float", sy
				, "float", sw
				, "float", sh
				, "int", 2
				, Ptr, ImageAttr
				, Ptr, 0
				, Ptr, 0)
	if ImageAttr
		Gdip_DisposeImageAttributes(ImageAttr)
	return E
}

;#####################################################################################

; Function				Gdip_SetImageAttributesColorMatrix
; Description			This function creates an image matrix ready for drawing
;
; Matrix				a matrix used to alter image attributes when drawing
;						passed with any delimeter
;
; return				returns an image matrix on sucess or 0 if it fails
;
; notes					MatrixBright = 1.5|0|0|0|0|0|1.5|0|0|0|0|0|1.5|0|0|0|0|0|1|0|0.05|0.05|0.05|0|1
;						MatrixGreyScale = 0.299|0.299|0.299|0|0|0.587|0.587|0.587|0|0|0.114|0.114|0.114|0|0|0|0|0|1|0|0|0|0|0|1
;						MatrixNegative = -1|0|0|0|0|0|-1|0|0|0|0|0|-1|0|0|0|0|0|1|0|0|0|0|0|1

Gdip_SetImageAttributesColorMatrix(Matrix)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	VarSetCapacity(ColourMatrix, 100, 0)
	Matrix := RegExReplace(RegExReplace(Matrix, "^[^\d-\.]+([\d\.])", "$1", "", 1), "[^\d-\.]+", "|")
	StringSplit, Matrix, Matrix, |
	Loop, 25
	{
		Matrix := (Matrix%A_Index% != "") ? Matrix%A_Index% : Mod(A_Index-1, 6) ? 0 : 1
		NumPut(Matrix, ColourMatrix, (A_Index-1)*4, "float")
	}
	DllCall("gdiplus\GdipCreateImageAttributes", A_PtrSize ? "UPtr*" : "uint*", ImageAttr)
	DllCall("gdiplus\GdipSetImageAttributesColorMatrix", Ptr, ImageAttr, "int", 1, "int", 1, Ptr, &ColourMatrix, Ptr, 0, "int", 0)
	return ImageAttr
}

;#####################################################################################

; Function				Gdip_GraphicsFromImage
; Description			This function gets the graphics for a bitmap used for drawing functions
;
; pBitmap				Pointer to a bitmap to get the pointer to its graphics
;
; return				returns a pointer to the graphics of a bitmap
;
; notes					a bitmap can be drawn into the graphics of another bitmap

Gdip_GraphicsFromImage(pBitmap)
{
	DllCall("gdiplus\GdipGetImageGraphicsContext", A_PtrSize ? "UPtr" : "UInt", pBitmap, A_PtrSize ? "UPtr*" : "UInt*", pGraphics)
	return pGraphics
}

;#####################################################################################

; Function				Gdip_GraphicsFromHDC
; Description			This function gets the graphics from the handle to a device context
;
; hdc					This is the handle to the device context
;
; return				returns a pointer to the graphics of a bitmap
;
; notes					You can draw a bitmap into the graphics of another bitmap

Gdip_GraphicsFromHDC(hdc)
{
	DllCall("gdiplus\GdipCreateFromHDC", A_PtrSize ? "UPtr" : "UInt", hdc, A_PtrSize ? "UPtr*" : "UInt*", pGraphics)
	return pGraphics
}

;#####################################################################################

; Function				Gdip_GetDC
; Description			This function gets the device context of the passed Graphics
;
; hdc					This is the handle to the device context
;
; return				returns the device context for the graphics of a bitmap

Gdip_GetDC(pGraphics)
{
	DllCall("gdiplus\GdipGetDC", A_PtrSize ? "UPtr" : "UInt", pGraphics, A_PtrSize ? "UPtr*" : "UInt*", hdc)
	return hdc
}

;#####################################################################################

; Function				Gdip_ReleaseDC
; Description			This function releases a device context from use for further use
;
; pGraphics				Pointer to the graphics of a bitmap
; hdc					This is the handle to the device context
;
; return				status enumeration. 0 = success

Gdip_ReleaseDC(pGraphics, hdc)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipReleaseDC", Ptr, pGraphics, Ptr, hdc)
}

;#####################################################################################

; Function				Gdip_GraphicsClear
; Description			Clears the graphics of a bitmap ready for further drawing
;
; pGraphics				Pointer to the graphics of a bitmap
; ARGB					The colour to clear the graphics to
;
; return				status enumeration. 0 = success
;
; notes					By default this will make the background invisible
;						Using clipping regions you can clear a particular area on the graphics rather than clearing the entire graphics

Gdip_GraphicsClear(pGraphics, ARGB=0x00ffffff)
{
	return DllCall("gdiplus\GdipGraphicsClear", A_PtrSize ? "UPtr" : "UInt", pGraphics, "int", ARGB)
}

;#####################################################################################

; Function				Gdip_BlurBitmap
; Description			Gives a pointer to a blurred bitmap from a pointer to a bitmap
;
; pBitmap				Pointer to a bitmap to be blurred
; Blur					The Amount to blur a bitmap by from 1 (least blur) to 100 (most blur)
;
; return				If the function succeeds, the return value is a pointer to the new blurred bitmap
;						-1 = The blur parameter is outside the range 1-100
;
; notes					This function will not dispose of the original bitmap

Gdip_BlurBitmap(pBitmap, Blur)
{
	if (Blur > 100) || (Blur < 1)
		return -1	
	
	sWidth := Gdip_GetImageWidth(pBitmap), sHeight := Gdip_GetImageHeight(pBitmap)
	dWidth := sWidth//Blur, dHeight := sHeight//Blur
	
	pBitmap1 := Gdip_CreateBitmap(dWidth, dHeight)
	G1 := Gdip_GraphicsFromImage(pBitmap1)
	Gdip_SetInterpolationMode(G1, 7)
	Gdip_DrawImage(G1, pBitmap, 0, 0, dWidth, dHeight, 0, 0, sWidth, sHeight)
	
	Gdip_DeleteGraphics(G1)
	
	pBitmap2 := Gdip_CreateBitmap(sWidth, sHeight)
	G2 := Gdip_GraphicsFromImage(pBitmap2)
	Gdip_SetInterpolationMode(G2, 7)
	Gdip_DrawImage(G2, pBitmap1, 0, 0, sWidth, sHeight, 0, 0, dWidth, dHeight)
	
	Gdip_DeleteGraphics(G2)
	Gdip_DisposeImage(pBitmap1)
	return pBitmap2
}

;#####################################################################################

; Function:     		Gdip_SaveBitmapToFile
; Description:  		Saves a bitmap to a file in any supported format onto disk
;   
; pBitmap				Pointer to a bitmap
; sOutput      			The name of the file that the bitmap will be saved to. Supported extensions are: .BMP,.DIB,.RLE,.JPG,.JPEG,.JPE,.JFIF,.GIF,.TIF,.TIFF,.PNG
; Quality      			If saving as jpg (.JPG,.JPEG,.JPE,.JFIF) then quality can be 1-100 with default at maximum quality
;
; return      			If the function succeeds, the return value is zero, otherwise:
;						-1 = Extension supplied is not a supported file format
;						-2 = Could not get a list of encoders on system
;						-3 = Could not find matching encoder for specified file format
;						-4 = Could not get WideChar name of output file
;						-5 = Could not save file to disk
;
; notes					This function will use the extension supplied from the sOutput parameter to determine the output format

Gdip_SaveBitmapToFile(pBitmap, sOutput, Quality=75)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	SplitPath, sOutput,,, Extension
	if Extension not in BMP,DIB,RLE,JPG,JPEG,JPE,JFIF,GIF,TIF,TIFF,PNG
		return -1
	Extension := "." Extension
	
	DllCall("gdiplus\GdipGetImageEncodersSize", "uint*", nCount, "uint*", nSize)
	VarSetCapacity(ci, nSize)
	DllCall("gdiplus\GdipGetImageEncoders", "uint", nCount, "uint", nSize, Ptr, &ci)
	if !(nCount && nSize)
		return -2
	
	If (A_IsUnicode){
		StrGet_Name := "StrGet"
		Loop, %nCount%
		{
			sString := %StrGet_Name%(NumGet(ci, (idx := (48+7*A_PtrSize)*(A_Index-1))+32+3*A_PtrSize), "UTF-16")
			if !InStr(sString, "*" Extension)
				continue
			
			pCodec := &ci+idx
			break
		}
	} else {
		Loop, %nCount%
		{
			Location := NumGet(ci, 76*(A_Index-1)+44)
			nSize := DllCall("WideCharToMultiByte", "uint", 0, "uint", 0, "uint", Location, "int", -1, "uint", 0, "int",  0, "uint", 0, "uint", 0)
			VarSetCapacity(sString, nSize)
			DllCall("WideCharToMultiByte", "uint", 0, "uint", 0, "uint", Location, "int", -1, "str", sString, "int", nSize, "uint", 0, "uint", 0)
			if !InStr(sString, "*" Extension)
				continue
			
			pCodec := &ci+76*(A_Index-1)
			break
		}
	}
	
	if !pCodec
		return -3
	
	if (Quality != 75)
	{
		Quality := (Quality < 0) ? 0 : (Quality > 100) ? 100 : Quality
		if Extension in .JPG,.JPEG,.JPE,.JFIF
		{
			DllCall("gdiplus\GdipGetEncoderParameterListSize", Ptr, pBitmap, Ptr, pCodec, "uint*", nSize)
			VarSetCapacity(EncoderParameters, nSize, 0)
			DllCall("gdiplus\GdipGetEncoderParameterList", Ptr, pBitmap, Ptr, pCodec, "uint", nSize, Ptr, &EncoderParameters)
			Loop, % NumGet(EncoderParameters, "UInt")      ;%
			{
				elem := (24+(A_PtrSize ? A_PtrSize : 4))*(A_Index-1) + 4 + (pad := A_PtrSize = 8 ? 4 : 0)
				if (NumGet(EncoderParameters, elem+16, "UInt") = 1) && (NumGet(EncoderParameters, elem+20, "UInt") = 6)
				{
					p := elem+&EncoderParameters-pad-4
					NumPut(Quality, NumGet(NumPut(4, NumPut(1, p+0)+20, "UInt")), "UInt")
					break
				}
			}      
		}
	}
	
	if (!A_IsUnicode)
	{
		nSize := DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sOutput, "int", -1, Ptr, 0, "int", 0)
		VarSetCapacity(wOutput, nSize*2)
		DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sOutput, "int", -1, Ptr, &wOutput, "int", nSize)
		VarSetCapacity(wOutput, -1)
		if !VarSetCapacity(wOutput)
			return -4
		E := DllCall("gdiplus\GdipSaveImageToFile", Ptr, pBitmap, Ptr, &wOutput, Ptr, pCodec, "uint", p ? p : 0)
	}
	else
		E := DllCall("gdiplus\GdipSaveImageToFile", Ptr, pBitmap, Ptr, &sOutput, Ptr, pCodec, "uint", p ? p : 0)
	return E ? -5 : 0
}

;#####################################################################################

; Function				Gdip_GetPixel
; Description			Gets the ARGB of a pixel in a bitmap
;
; pBitmap				Pointer to a bitmap
; x						x-coordinate of the pixel
; y						y-coordinate of the pixel
;
; return				Returns the ARGB value of the pixel

Gdip_GetPixel(pBitmap, x, y)
{
	DllCall("gdiplus\GdipBitmapGetPixel", A_PtrSize ? "UPtr" : "UInt", pBitmap, "int", x, "int", y, "uint*", ARGB)
	return ARGB
}

;#####################################################################################

; Function				Gdip_SetPixel
; Description			Sets the ARGB of a pixel in a bitmap
;
; pBitmap				Pointer to a bitmap
; x						x-coordinate of the pixel
; y						y-coordinate of the pixel
;
; return				status enumeration. 0 = success

Gdip_SetPixel(pBitmap, x, y, ARGB)
{
	return DllCall("gdiplus\GdipBitmapSetPixel", A_PtrSize ? "UPtr" : "UInt", pBitmap, "int", x, "int", y, "int", ARGB)
}

;#####################################################################################

; Function				Gdip_GetImageWidth
; Description			Gives the width of a bitmap
;
; pBitmap				Pointer to a bitmap
;
; return				Returns the width in pixels of the supplied bitmap

Gdip_GetImageWidth(pBitmap)
{
	DllCall("gdiplus\GdipGetImageWidth", A_PtrSize ? "UPtr" : "UInt", pBitmap, "uint*", Width)
	return Width
}

;#####################################################################################

; Function				Gdip_GetImageHeight
; Description			Gives the height of a bitmap
;
; pBitmap				Pointer to a bitmap
;
; return				Returns the height in pixels of the supplied bitmap

Gdip_GetImageHeight(pBitmap)
{
	DllCall("gdiplus\GdipGetImageHeight", A_PtrSize ? "UPtr" : "UInt", pBitmap, "uint*", Height)
	return Height
}

;#####################################################################################

; Function				Gdip_GetDimensions
; Description			Gives the width and height of a bitmap
;
; pBitmap				Pointer to a bitmap
; Width					ByRef variable. This variable will be set to the width of the bitmap
; Height				ByRef variable. This variable will be set to the height of the bitmap
;
; return				No return value
;						Gdip_GetDimensions(pBitmap, ThisWidth, ThisHeight) will set ThisWidth to the width and ThisHeight to the height

Gdip_GetImageDimensions(pBitmap, ByRef Width, ByRef Height)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	DllCall("gdiplus\GdipGetImageWidth", Ptr, pBitmap, "uint*", Width)
	DllCall("gdiplus\GdipGetImageHeight", Ptr, pBitmap, "uint*", Height)
}

;#####################################################################################

Gdip_GetDimensions(pBitmap, ByRef Width, ByRef Height)
{
	Gdip_GetImageDimensions(pBitmap, Width, Height)
}

;#####################################################################################

Gdip_GetImagePixelFormat(pBitmap)
{
	DllCall("gdiplus\GdipGetImagePixelFormat", A_PtrSize ? "UPtr" : "UInt", pBitmap, A_PtrSize ? "UPtr*" : "UInt*", Format)
	return Format
}

;#####################################################################################

; Function				Gdip_GetDpiX
; Description			Gives the horizontal dots per inch of the graphics of a bitmap
;
; pBitmap				Pointer to a bitmap
; Width					ByRef variable. This variable will be set to the width of the bitmap
; Height				ByRef variable. This variable will be set to the height of the bitmap
;
; return				No return value
;						Gdip_GetDimensions(pBitmap, ThisWidth, ThisHeight) will set ThisWidth to the width and ThisHeight to the height

Gdip_GetDpiX(pGraphics)
{
	DllCall("gdiplus\GdipGetDpiX", A_PtrSize ? "UPtr" : "uint", pGraphics, "float*", dpix)
	return Round(dpix)
}

;#####################################################################################

Gdip_GetDpiY(pGraphics)
{
	DllCall("gdiplus\GdipGetDpiY", A_PtrSize ? "UPtr" : "uint", pGraphics, "float*", dpiy)
	return Round(dpiy)
}

;#####################################################################################

Gdip_GetImageHorizontalResolution(pBitmap)
{
	DllCall("gdiplus\GdipGetImageHorizontalResolution", A_PtrSize ? "UPtr" : "uint", pBitmap, "float*", dpix)
	return Round(dpix)
}

;#####################################################################################

Gdip_GetImageVerticalResolution(pBitmap)
{
	DllCall("gdiplus\GdipGetImageVerticalResolution", A_PtrSize ? "UPtr" : "uint", pBitmap, "float*", dpiy)
	return Round(dpiy)
}

;#####################################################################################

Gdip_BitmapSetResolution(pBitmap, dpix, dpiy)
{
	return DllCall("gdiplus\GdipBitmapSetResolution", A_PtrSize ? "UPtr" : "uint", pBitmap, "float", dpix, "float", dpiy)
}

;#####################################################################################

Gdip_CreateBitmapFromFile(sFile, IconNumber=1, IconSize="")
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	, PtrA := A_PtrSize ? "UPtr*" : "UInt*"
	
	SplitPath, sFile,,, ext
	if ext in exe,dll
	{
		Sizes := IconSize ? IconSize : 256 "|" 128 "|" 64 "|" 48 "|" 32 "|" 16
		BufSize := 16 + (2*(A_PtrSize ? A_PtrSize : 4))
		
		VarSetCapacity(buf, BufSize, 0)
		Loop, Parse, Sizes, |
		{
			DllCall("PrivateExtractIcons", "str", sFile, "int", IconNumber-1, "int", A_LoopField, "int", A_LoopField, PtrA, hIcon, PtrA, 0, "uint", 1, "uint", 0)
			
			if !hIcon
				continue
			
			if !DllCall("GetIconInfo", Ptr, hIcon, Ptr, &buf)
			{
				DestroyIcon(hIcon)
				continue
			}
			
			hbmMask  := NumGet(buf, 12 + ((A_PtrSize ? A_PtrSize : 4) - 4))
			hbmColor := NumGet(buf, 12 + ((A_PtrSize ? A_PtrSize : 4) - 4) + (A_PtrSize ? A_PtrSize : 4))
			if !(hbmColor && DllCall("GetObject", Ptr, hbmColor, "int", BufSize, Ptr, &buf))
			{
				DestroyIcon(hIcon)
				continue
			}
			break
		}
		if !hIcon
			return -1
		
		Width := NumGet(buf, 4, "int"), Height := NumGet(buf, 8, "int")
		hbm := CreateDIBSection(Width, -Height), hdc := CreateCompatibleDC(), obm := SelectObject(hdc, hbm)
		if !DllCall("DrawIconEx", Ptr, hdc, "int", 0, "int", 0, Ptr, hIcon, "uint", Width, "uint", Height, "uint", 0, Ptr, 0, "uint", 3)
		{
			DestroyIcon(hIcon)
			return -2
		}
		
		VarSetCapacity(dib, 104)
		DllCall("GetObject", Ptr, hbm, "int", A_PtrSize = 8 ? 104 : 84, Ptr, &dib) ; sizeof(DIBSECTION) = 76+2*(A_PtrSize=8?4:0)+2*A_PtrSize
		Stride := NumGet(dib, 12, "Int"), Bits := NumGet(dib, 20 + (A_PtrSize = 8 ? 4 : 0)) ; padding
		DllCall("gdiplus\GdipCreateBitmapFromScan0", "int", Width, "int", Height, "int", Stride, "int", 0x26200A, Ptr, Bits, PtrA, pBitmapOld)
		pBitmap := Gdip_CreateBitmap(Width, Height)
		G := Gdip_GraphicsFromImage(pBitmap)
		, Gdip_DrawImage(G, pBitmapOld, 0, 0, Width, Height, 0, 0, Width, Height)
		SelectObject(hdc, obm), DeleteObject(hbm), DeleteDC(hdc)
		Gdip_DeleteGraphics(G), Gdip_DisposeImage(pBitmapOld)
		DestroyIcon(hIcon)
	}
	else
	{
		if (!A_IsUnicode)
		{
			VarSetCapacity(wFile, 1024)
			DllCall("kernel32\MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sFile, "int", -1, Ptr, &wFile, "int", 512)
			DllCall("gdiplus\GdipCreateBitmapFromFile", Ptr, &wFile, PtrA, pBitmap)
		}
		else
			DllCall("gdiplus\GdipCreateBitmapFromFile", Ptr, &sFile, PtrA, pBitmap)
	}
	
	return pBitmap
}

;#####################################################################################

Gdip_CreateBitmapFromHBITMAP(hBitmap, Palette=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	DllCall("gdiplus\GdipCreateBitmapFromHBITMAP", Ptr, hBitmap, Ptr, Palette, A_PtrSize ? "UPtr*" : "uint*", pBitmap)
	return pBitmap
}

;#####################################################################################

Gdip_CreateHBITMAPFromBitmap(pBitmap, Background=0xffffffff)
{
	DllCall("gdiplus\GdipCreateHBITMAPFromBitmap", A_PtrSize ? "UPtr" : "UInt", pBitmap, A_PtrSize ? "UPtr*" : "uint*", hbm, "int", Background)
	return hbm
}

;#####################################################################################

Gdip_CreateBitmapFromHICON(hIcon)
{
	DllCall("gdiplus\GdipCreateBitmapFromHICON", A_PtrSize ? "UPtr" : "UInt", hIcon, A_PtrSize ? "UPtr*" : "uint*", pBitmap)
	return pBitmap
}

;#####################################################################################

Gdip_CreateHICONFromBitmap(pBitmap)
{
	DllCall("gdiplus\GdipCreateHICONFromBitmap", A_PtrSize ? "UPtr" : "UInt", pBitmap, A_PtrSize ? "UPtr*" : "uint*", hIcon)
	return hIcon
}

;#####################################################################################

Gdip_CreateBitmap(Width, Height, Format=0x26200A)
{
	DllCall("gdiplus\GdipCreateBitmapFromScan0", "int", Width, "int", Height, "int", 0, "int", Format, A_PtrSize ? "UPtr" : "UInt", 0, A_PtrSize ? "UPtr*" : "uint*", pBitmap)
	Return pBitmap
}

;#####################################################################################

Gdip_CreateBitmapFromClipboard()
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if !DllCall("OpenClipboard", Ptr, 0)
		return -1
	if !DllCall("IsClipboardFormatAvailable", "uint", 8)
		return -2
	if !hBitmap := DllCall("GetClipboardData", "uint", 2, Ptr)
		return -3
	if !pBitmap := Gdip_CreateBitmapFromHBITMAP(hBitmap)
		return -4
	if !DllCall("CloseClipboard")
		return -5
	DeleteObject(hBitmap)
	return pBitmap
}

;#####################################################################################

Gdip_SetBitmapToClipboard(pBitmap)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	off1 := A_PtrSize = 8 ? 52 : 44, off2 := A_PtrSize = 8 ? 32 : 24
	hBitmap := Gdip_CreateHBITMAPFromBitmap(pBitmap)
	DllCall("GetObject", Ptr, hBitmap, "int", VarSetCapacity(oi, A_PtrSize = 8 ? 104 : 84, 0), Ptr, &oi)
	hdib := DllCall("GlobalAlloc", "uint", 2, Ptr, 40+NumGet(oi, off1, "UInt"), Ptr)
	pdib := DllCall("GlobalLock", Ptr, hdib, Ptr)
	DllCall("RtlMoveMemory", Ptr, pdib, Ptr, &oi+off2, Ptr, 40)
	DllCall("RtlMoveMemory", Ptr, pdib+40, Ptr, NumGet(oi, off2 - (A_PtrSize ? A_PtrSize : 4), Ptr), Ptr, NumGet(oi, off1, "UInt"))
	DllCall("GlobalUnlock", Ptr, hdib)
	DllCall("DeleteObject", Ptr, hBitmap)
	DllCall("OpenClipboard", Ptr, 0)
	DllCall("EmptyClipboard")
	DllCall("SetClipboardData", "uint", 8, Ptr, hdib)
	DllCall("CloseClipboard")
}

;#####################################################################################

Gdip_CloneBitmapArea(pBitmap, x, y, w, h, Format=0x26200A)
{
	DllCall("gdiplus\GdipCloneBitmapArea"
					, "float", x
					, "float", y
					, "float", w
					, "float", h
					, "int", Format
					, A_PtrSize ? "UPtr" : "UInt", pBitmap
					, A_PtrSize ? "UPtr*" : "UInt*", pBitmapDest)
	return pBitmapDest
}

;#####################################################################################
; Create resources
;#####################################################################################

Gdip_CreatePen(ARGB, w)
{
	DllCall("gdiplus\GdipCreatePen1", "UInt", ARGB, "float", w, "int", 2, A_PtrSize ? "UPtr*" : "UInt*", pPen)
	return pPen
}

;#####################################################################################

Gdip_CreatePenFromBrush(pBrush, w)
{
	DllCall("gdiplus\GdipCreatePen2", A_PtrSize ? "UPtr" : "UInt", pBrush, "float", w, "int", 2, A_PtrSize ? "UPtr*" : "UInt*", pPen)
	return pPen
}

;#####################################################################################

Gdip_BrushCreateSolid(ARGB=0xff000000)
{
	DllCall("gdiplus\GdipCreateSolidFill", "UInt", ARGB, A_PtrSize ? "UPtr*" : "UInt*", pBrush)
	return pBrush
}

;#####################################################################################

; HatchStyleHorizontal = 0
; HatchStyleVertical = 1
; HatchStyleForwardDiagonal = 2
; HatchStyleBackwardDiagonal = 3
; HatchStyleCross = 4
; HatchStyleDiagonalCross = 5
; HatchStyle05Percent = 6
; HatchStyle10Percent = 7
; HatchStyle20Percent = 8
; HatchStyle25Percent = 9
; HatchStyle30Percent = 10
; HatchStyle40Percent = 11
; HatchStyle50Percent = 12
; HatchStyle60Percent = 13
; HatchStyle70Percent = 14
; HatchStyle75Percent = 15
; HatchStyle80Percent = 16
; HatchStyle90Percent = 17
; HatchStyleLightDownwardDiagonal = 18
; HatchStyleLightUpwardDiagonal = 19
; HatchStyleDarkDownwardDiagonal = 20
; HatchStyleDarkUpwardDiagonal = 21
; HatchStyleWideDownwardDiagonal = 22
; HatchStyleWideUpwardDiagonal = 23
; HatchStyleLightVertical = 24
; HatchStyleLightHorizontal = 25
; HatchStyleNarrowVertical = 26
; HatchStyleNarrowHorizontal = 27
; HatchStyleDarkVertical = 28
; HatchStyleDarkHorizontal = 29
; HatchStyleDashedDownwardDiagonal = 30
; HatchStyleDashedUpwardDiagonal = 31
; HatchStyleDashedHorizontal = 32
; HatchStyleDashedVertical = 33
; HatchStyleSmallConfetti = 34
; HatchStyleLargeConfetti = 35
; HatchStyleZigZag = 36
; HatchStyleWave = 37
; HatchStyleDiagonalBrick = 38
; HatchStyleHorizontalBrick = 39
; HatchStyleWeave = 40
; HatchStylePlaid = 41
; HatchStyleDivot = 42
; HatchStyleDottedGrid = 43
; HatchStyleDottedDiamond = 44
; HatchStyleShingle = 45
; HatchStyleTrellis = 46
; HatchStyleSphere = 47
; HatchStyleSmallGrid = 48
; HatchStyleSmallCheckerBoard = 49
; HatchStyleLargeCheckerBoard = 50
; HatchStyleOutlinedDiamond = 51
; HatchStyleSolidDiamond = 52
; HatchStyleTotal = 53
Gdip_BrushCreateHatch(ARGBfront, ARGBback, HatchStyle=0)
{
	DllCall("gdiplus\GdipCreateHatchBrush", "int", HatchStyle, "UInt", ARGBfront, "UInt", ARGBback, A_PtrSize ? "UPtr*" : "UInt*", pBrush)
	return pBrush
}

;#####################################################################################

Gdip_CreateTextureBrush(pBitmap, WrapMode=1, x=0, y=0, w="", h="")
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	, PtrA := A_PtrSize ? "UPtr*" : "UInt*"
	
	if !(w && h)
		DllCall("gdiplus\GdipCreateTexture", Ptr, pBitmap, "int", WrapMode, PtrA, pBrush)
	else
		DllCall("gdiplus\GdipCreateTexture2", Ptr, pBitmap, "int", WrapMode, "float", x, "float", y, "float", w, "float", h, PtrA, pBrush)
	return pBrush
}

;#####################################################################################

; WrapModeTile = 0
; WrapModeTileFlipX = 1
; WrapModeTileFlipY = 2
; WrapModeTileFlipXY = 3
; WrapModeClamp = 4
Gdip_CreateLineBrush(x1, y1, x2, y2, ARGB1, ARGB2, WrapMode=1)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	CreatePointF(PointF1, x1, y1), CreatePointF(PointF2, x2, y2)
	DllCall("gdiplus\GdipCreateLineBrush", Ptr, &PointF1, Ptr, &PointF2, "Uint", ARGB1, "Uint", ARGB2, "int", WrapMode, A_PtrSize ? "UPtr*" : "UInt*", LGpBrush)
	return LGpBrush
}

;#####################################################################################

; LinearGradientModeHorizontal = 0
; LinearGradientModeVertical = 1
; LinearGradientModeForwardDiagonal = 2
; LinearGradientModeBackwardDiagonal = 3
Gdip_CreateLineBrushFromRect(x, y, w, h, ARGB1, ARGB2, LinearGradientMode=1, WrapMode=1)
{
	CreateRectF(RectF, x, y, w, h)
	DllCall("gdiplus\GdipCreateLineBrushFromRect", A_PtrSize ? "UPtr" : "UInt", &RectF, "int", ARGB1, "int", ARGB2, "int", LinearGradientMode, "int", WrapMode, A_PtrSize ? "UPtr*" : "UInt*", LGpBrush)
	return LGpBrush
}

;#####################################################################################

Gdip_CloneBrush(pBrush)
{
	DllCall("gdiplus\GdipCloneBrush", A_PtrSize ? "UPtr" : "UInt", pBrush, A_PtrSize ? "UPtr*" : "UInt*", pBrushClone)
	return pBrushClone
}

;#####################################################################################
; Delete resources
;#####################################################################################

Gdip_DeletePen(pPen)
{
	return DllCall("gdiplus\GdipDeletePen", A_PtrSize ? "UPtr" : "UInt", pPen)
}

;#####################################################################################

Gdip_DeleteBrush(pBrush)
{
	return DllCall("gdiplus\GdipDeleteBrush", A_PtrSize ? "UPtr" : "UInt", pBrush)
}

;#####################################################################################

Gdip_DisposeImage(pBitmap)
{
	return DllCall("gdiplus\GdipDisposeImage", A_PtrSize ? "UPtr" : "UInt", pBitmap)
}

;#####################################################################################

Gdip_DeleteGraphics(pGraphics)
{
	return DllCall("gdiplus\GdipDeleteGraphics", A_PtrSize ? "UPtr" : "UInt", pGraphics)
}

;#####################################################################################

Gdip_DisposeImageAttributes(ImageAttr)
{
	return DllCall("gdiplus\GdipDisposeImageAttributes", A_PtrSize ? "UPtr" : "UInt", ImageAttr)
}

;#####################################################################################

Gdip_DeleteFont(hFont)
{
	return DllCall("gdiplus\GdipDeleteFont", A_PtrSize ? "UPtr" : "UInt", hFont)
}

;#####################################################################################

Gdip_DeleteStringFormat(hFormat)
{
	return DllCall("gdiplus\GdipDeleteStringFormat", A_PtrSize ? "UPtr" : "UInt", hFormat)
}

;#####################################################################################

Gdip_DeleteFontFamily(hFamily)
{
	return DllCall("gdiplus\GdipDeleteFontFamily", A_PtrSize ? "UPtr" : "UInt", hFamily)
}

;#####################################################################################

Gdip_DeleteMatrix(Matrix)
{
	return DllCall("gdiplus\GdipDeleteMatrix", A_PtrSize ? "UPtr" : "UInt", Matrix)
}

;#####################################################################################
; Text functions
;#####################################################################################

Gdip_TextToGraphics(pGraphics, Text, Options, Font="Arial", Width="", Height="", Measure=0)
{
	IWidth := Width, IHeight:= Height
	
	RegExMatch(Options, "i)X([\-\d\.]+)(p*)", xpos)
	RegExMatch(Options, "i)Y([\-\d\.]+)(p*)", ypos)
	RegExMatch(Options, "i)W([\-\d\.]+)(p*)", Width)
	RegExMatch(Options, "i)H([\-\d\.]+)(p*)", Height)
	RegExMatch(Options, "i)C(?!(entre|enter))([a-f\d]+)", Colour)
	RegExMatch(Options, "i)Top|Up|Bottom|Down|vCentre|vCenter", vPos)
	RegExMatch(Options, "i)NoWrap", NoWrap)
	RegExMatch(Options, "i)R(\d)", Rendering)
	RegExMatch(Options, "i)S(\d+)(p*)", Size)
	
	if !Gdip_DeleteBrush(Gdip_CloneBrush(Colour2))
		PassBrush := 1, pBrush := Colour2
	
	if !(IWidth && IHeight) && (xpos2 || ypos2 || Width2 || Height2 || Size2)
		return -1
	
	Style := 0, Styles := "Regular|Bold|Italic|BoldItalic|Underline|Strikeout"
	Loop, Parse, Styles, |
	{
		if RegExMatch(Options, "\b" A_loopField)
			Style |= (A_LoopField != "StrikeOut") ? (A_Index-1) : 8
	}
	
	Align := 0, Alignments := "Near|Left|Centre|Center|Far|Right"
	Loop, Parse, Alignments, |
	{
		if RegExMatch(Options, "\b" A_loopField)
			Align |= A_Index//2.1      ; 0|0|1|1|2|2
	}
	
	xpos := (xpos1 != "") ? xpos2 ? IWidth*(xpos1/100) : xpos1 : 0
	ypos := (ypos1 != "") ? ypos2 ? IHeight*(ypos1/100) : ypos1 : 0
	Width := Width1 ? Width2 ? IWidth*(Width1/100) : Width1 : IWidth
	Height := Height1 ? Height2 ? IHeight*(Height1/100) : Height1 : IHeight
	if !PassBrush
		Colour := "0x" (Colour2 ? Colour2 : "ff000000")
	Rendering := ((Rendering1 >= 0) && (Rendering1 <= 5)) ? Rendering1 : 4
	Size := (Size1 > 0) ? Size2 ? IHeight*(Size1/100) : Size1 : 12
	
	hFamily := Gdip_FontFamilyCreate(Font)
	hFont := Gdip_FontCreate(hFamily, Size, Style)
	FormatStyle := NoWrap ? 0x4000 | 0x1000 : 0x4000
	hFormat := Gdip_StringFormatCreate(FormatStyle)
	pBrush := PassBrush ? pBrush : Gdip_BrushCreateSolid(Colour)
	if !(hFamily && hFont && hFormat && pBrush && pGraphics)
		return !pGraphics ? -2 : !hFamily ? -3 : !hFont ? -4 : !hFormat ? -5 : !pBrush ? -6 : 0
	
	CreateRectF(RC, xpos, ypos, Width, Height)
	Gdip_SetStringFormatAlign(hFormat, Align)
	Gdip_SetTextRenderingHint(pGraphics, Rendering)
	ReturnRC := Gdip_MeasureString(pGraphics, Text, hFont, hFormat, RC)
	
	if vPos
	{
		StringSplit, ReturnRC, ReturnRC, |
		
		if (vPos = "vCentre") || (vPos = "vCenter")
			ypos += (Height-ReturnRC4)//2
		else if (vPos = "Top") || (vPos = "Up")
			ypos := 0
		else if (vPos = "Bottom") || (vPos = "Down")
			ypos := Height-ReturnRC4
		
		CreateRectF(RC, xpos, ypos, Width, ReturnRC4)
		ReturnRC := Gdip_MeasureString(pGraphics, Text, hFont, hFormat, RC)
	}
	
	if !Measure
		E := Gdip_DrawString(pGraphics, Text, hFont, hFormat, pBrush, RC)
	
	if !PassBrush
		Gdip_DeleteBrush(pBrush)
	Gdip_DeleteStringFormat(hFormat)   
	Gdip_DeleteFont(hFont)
	Gdip_DeleteFontFamily(hFamily)
	return E ? E : ReturnRC
}

;#####################################################################################

Gdip_DrawString(pGraphics, sString, hFont, hFormat, pBrush, ByRef RectF)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if (!A_IsUnicode)
	{
		nSize := DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sString, "int", -1, Ptr, 0, "int", 0)
		VarSetCapacity(wString, nSize*2)
		DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sString, "int", -1, Ptr, &wString, "int", nSize)
	}
	
	return DllCall("gdiplus\GdipDrawString"
					, Ptr, pGraphics
					, Ptr, A_IsUnicode ? &sString : &wString
					, "int", -1
					, Ptr, hFont
					, Ptr, &RectF
					, Ptr, hFormat
					, Ptr, pBrush)
}

;#####################################################################################

Gdip_MeasureString(pGraphics, sString, hFont, hFormat, ByRef RectF)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	VarSetCapacity(RC, 16)
	if !A_IsUnicode
	{
		nSize := DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sString, "int", -1, "uint", 0, "int", 0)
		VarSetCapacity(wString, nSize*2)   
		DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &sString, "int", -1, Ptr, &wString, "int", nSize)
	}
	
	DllCall("gdiplus\GdipMeasureString"
					, Ptr, pGraphics
					, Ptr, A_IsUnicode ? &sString : &wString
					, "int", -1
					, Ptr, hFont
					, Ptr, &RectF
					, Ptr, hFormat
					, Ptr, &RC
					, "uint*", Chars
					, "uint*", Lines)
	
	return &RC ? NumGet(RC, 0, "float") "|" NumGet(RC, 4, "float") "|" NumGet(RC, 8, "float") "|" NumGet(RC, 12, "float") "|" Chars "|" Lines : 0
}

; Near = 0
; Center = 1
; Far = 2
Gdip_SetStringFormatAlign(hFormat, Align)
{
	return DllCall("gdiplus\GdipSetStringFormatAlign", A_PtrSize ? "UPtr" : "UInt", hFormat, "int", Align)
}

; StringFormatFlagsDirectionRightToLeft    = 0x00000001
; StringFormatFlagsDirectionVertical       = 0x00000002
; StringFormatFlagsNoFitBlackBox           = 0x00000004
; StringFormatFlagsDisplayFormatControl    = 0x00000020
; StringFormatFlagsNoFontFallback          = 0x00000400
; StringFormatFlagsMeasureTrailingSpaces   = 0x00000800
; StringFormatFlagsNoWrap                  = 0x00001000
; StringFormatFlagsLineLimit               = 0x00002000
; StringFormatFlagsNoClip                  = 0x00004000 
Gdip_StringFormatCreate(Format=0, Lang=0)
{
	DllCall("gdiplus\GdipCreateStringFormat", "int", Format, "int", Lang, A_PtrSize ? "UPtr*" : "UInt*", hFormat)
	return hFormat
}

; Regular = 0
; Bold = 1
; Italic = 2
; BoldItalic = 3
; Underline = 4
; Strikeout = 8
Gdip_FontCreate(hFamily, Size, Style=0)
{
	DllCall("gdiplus\GdipCreateFont", A_PtrSize ? "UPtr" : "UInt", hFamily, "float", Size, "int", Style, "int", 0, A_PtrSize ? "UPtr*" : "UInt*", hFont)
	return hFont
}

Gdip_FontFamilyCreate(Font)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if (!A_IsUnicode)
	{
		nSize := DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &Font, "int", -1, "uint", 0, "int", 0)
		VarSetCapacity(wFont, nSize*2)
		DllCall("MultiByteToWideChar", "uint", 0, "uint", 0, Ptr, &Font, "int", -1, Ptr, &wFont, "int", nSize)
	}
	
	DllCall("gdiplus\GdipCreateFontFamilyFromName"
					, Ptr, A_IsUnicode ? &Font : &wFont
					, "uint", 0
					, A_PtrSize ? "UPtr*" : "UInt*", hFamily)
	
	return hFamily
}

;#####################################################################################
; Matrix functions
;#####################################################################################

Gdip_CreateAffineMatrix(m11, m12, m21, m22, x, y)
{
	DllCall("gdiplus\GdipCreateMatrix2", "float", m11, "float", m12, "float", m21, "float", m22, "float", x, "float", y, A_PtrSize ? "UPtr*" : "UInt*", Matrix)
	return Matrix
}

Gdip_CreateMatrix()
{
	DllCall("gdiplus\GdipCreateMatrix", A_PtrSize ? "UPtr*" : "UInt*", Matrix)
	return Matrix
}

;#####################################################################################
; GraphicsPath functions
;#####################################################################################

; Alternate = 0
; Winding = 1
Gdip_CreatePath(BrushMode=0)
{
	DllCall("gdiplus\GdipCreatePath", "int", BrushMode, A_PtrSize ? "UPtr*" : "UInt*", Path)
	return Path
}

Gdip_AddPathEllipse(Path, x, y, w, h)
{
	return DllCall("gdiplus\GdipAddPathEllipse", A_PtrSize ? "UPtr" : "UInt", Path, "float", x, "float", y, "float", w, "float", h)
}

Gdip_AddPathPolygon(Path, Points)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	StringSplit, Points, Points, |
	VarSetCapacity(PointF, 8*Points0)   
	Loop, %Points0%
	{
		StringSplit, Coord, Points%A_Index%, `,
		NumPut(Coord1, PointF, 8*(A_Index-1), "float"), NumPut(Coord2, PointF, (8*(A_Index-1))+4, "float")
	}   
	
	return DllCall("gdiplus\GdipAddPathPolygon", Ptr, Path, Ptr, &PointF, "int", Points0)
}

Gdip_DeletePath(Path)
{
	return DllCall("gdiplus\GdipDeletePath", A_PtrSize ? "UPtr" : "UInt", Path)
}

;#####################################################################################
; Quality functions
;#####################################################################################

; SystemDefault = 0
; SingleBitPerPixelGridFit = 1
; SingleBitPerPixel = 2
; AntiAliasGridFit = 3
; AntiAlias = 4
Gdip_SetTextRenderingHint(pGraphics, RenderingHint)
{
	return DllCall("gdiplus\GdipSetTextRenderingHint", A_PtrSize ? "UPtr" : "UInt", pGraphics, "int", RenderingHint)
}

; Default = 0
; LowQuality = 1
; HighQuality = 2
; Bilinear = 3
; Bicubic = 4
; NearestNeighbor = 5
; HighQualityBilinear = 6
; HighQualityBicubic = 7
Gdip_SetInterpolationMode(pGraphics, InterpolationMode)
{
	return DllCall("gdiplus\GdipSetInterpolationMode", A_PtrSize ? "UPtr" : "UInt", pGraphics, "int", InterpolationMode)
}

; Default = 0
; HighSpeed = 1
; HighQuality = 2
; None = 3
; AntiAlias = 4
Gdip_SetSmoothingMode(pGraphics, SmoothingMode)
{
	return DllCall("gdiplus\GdipSetSmoothingMode", A_PtrSize ? "UPtr" : "UInt", pGraphics, "int", SmoothingMode)
}

; CompositingModeSourceOver = 0 (blended)
; CompositingModeSourceCopy = 1 (overwrite)
Gdip_SetCompositingMode(pGraphics, CompositingMode=0)
{
	return DllCall("gdiplus\GdipSetCompositingMode", A_PtrSize ? "UPtr" : "UInt", pGraphics, "int", CompositingMode)
}

;#####################################################################################
; Extra functions
;#####################################################################################

Gdip_Startup()
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if !DllCall("GetModuleHandle", "str", "gdiplus", Ptr)
		DllCall("LoadLibrary", "str", "gdiplus")
	VarSetCapacity(si, A_PtrSize = 8 ? 24 : 16, 0), si := Chr(1)
	DllCall("gdiplus\GdiplusStartup", A_PtrSize ? "UPtr*" : "uint*", pToken, Ptr, &si, Ptr, 0)
	return pToken
}

Gdip_Shutdown(pToken)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	DllCall("gdiplus\GdiplusShutdown", Ptr, pToken)
	if hModule := DllCall("GetModuleHandle", "str", "gdiplus", Ptr)
		DllCall("FreeLibrary", Ptr, hModule)
	return 0
}

; Prepend = 0; The new operation is applied before the old operation.
; Append = 1; The new operation is applied after the old operation.
Gdip_RotateWorldTransform(pGraphics, Angle, MatrixOrder=0)
{
	return DllCall("gdiplus\GdipRotateWorldTransform", A_PtrSize ? "UPtr" : "UInt", pGraphics, "float", Angle, "int", MatrixOrder)
}

Gdip_ScaleWorldTransform(pGraphics, x, y, MatrixOrder=0)
{
	return DllCall("gdiplus\GdipScaleWorldTransform", A_PtrSize ? "UPtr" : "UInt", pGraphics, "float", x, "float", y, "int", MatrixOrder)
}

Gdip_TranslateWorldTransform(pGraphics, x, y, MatrixOrder=0)
{
	return DllCall("gdiplus\GdipTranslateWorldTransform", A_PtrSize ? "UPtr" : "UInt", pGraphics, "float", x, "float", y, "int", MatrixOrder)
}

Gdip_ResetWorldTransform(pGraphics)
{
	return DllCall("gdiplus\GdipResetWorldTransform", A_PtrSize ? "UPtr" : "UInt", pGraphics)
}

Gdip_GetRotatedTranslation(Width, Height, Angle, ByRef xTranslation, ByRef yTranslation)
{
	pi := 3.14159, TAngle := Angle*(pi/180)	
	
	Bound := (Angle >= 0) ? Mod(Angle, 360) : 360-Mod(-Angle, -360)
	if ((Bound >= 0) && (Bound <= 90))
		xTranslation := Height*Sin(TAngle), yTranslation := 0
	else if ((Bound > 90) && (Bound <= 180))
		xTranslation := (Height*Sin(TAngle))-(Width*Cos(TAngle)), yTranslation := -Height*Cos(TAngle)
	else if ((Bound > 180) && (Bound <= 270))
		xTranslation := -(Width*Cos(TAngle)), yTranslation := -(Height*Cos(TAngle))-(Width*Sin(TAngle))
	else if ((Bound > 270) && (Bound <= 360))
		xTranslation := 0, yTranslation := -Width*Sin(TAngle)
}

Gdip_GetRotatedDimensions(Width, Height, Angle, ByRef RWidth, ByRef RHeight)
{
	pi := 3.14159, TAngle := Angle*(pi/180)
	if !(Width && Height)
		return -1
	RWidth := Ceil(Abs(Width*Cos(TAngle))+Abs(Height*Sin(TAngle)))
	RHeight := Ceil(Abs(Width*Sin(TAngle))+Abs(Height*Cos(Tangle)))
}

; RotateNoneFlipNone   = 0
; Rotate90FlipNone     = 1
; Rotate180FlipNone    = 2
; Rotate270FlipNone    = 3
; RotateNoneFlipX      = 4
; Rotate90FlipX        = 5
; Rotate180FlipX       = 6
; Rotate270FlipX       = 7
; RotateNoneFlipY      = Rotate180FlipX
; Rotate90FlipY        = Rotate270FlipX
; Rotate180FlipY       = RotateNoneFlipX
; Rotate270FlipY       = Rotate90FlipX
; RotateNoneFlipXY     = Rotate180FlipNone
; Rotate90FlipXY       = Rotate270FlipNone
; Rotate180FlipXY      = RotateNoneFlipNone
; Rotate270FlipXY      = Rotate90FlipNone 

Gdip_ImageRotateFlip(pBitmap, RotateFlipType=1)
{
	return DllCall("gdiplus\GdipImageRotateFlip", A_PtrSize ? "UPtr" : "UInt", pBitmap, "int", RotateFlipType)
}

; Replace = 0
; Intersect = 1
; Union = 2
; Xor = 3
; Exclude = 4
; Complement = 5
Gdip_SetClipRect(pGraphics, x, y, w, h, CombineMode=0)
{
	return DllCall("gdiplus\GdipSetClipRect",  A_PtrSize ? "UPtr" : "UInt", pGraphics, "float", x, "float", y, "float", w, "float", h, "int", CombineMode)
}

Gdip_SetClipPath(pGraphics, Path, CombineMode=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	return DllCall("gdiplus\GdipSetClipPath", Ptr, pGraphics, Ptr, Path, "int", CombineMode)
}

Gdip_ResetClip(pGraphics)
{
	return DllCall("gdiplus\GdipResetClip", A_PtrSize ? "UPtr" : "UInt", pGraphics)
}

Gdip_GetClipRegion(pGraphics)
{
	Region := Gdip_CreateRegion()
	DllCall("gdiplus\GdipGetClip", A_PtrSize ? "UPtr" : "UInt", pGraphics, "UInt*", Region)
	return Region
}

Gdip_SetClipRegion(pGraphics, Region, CombineMode=0)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("gdiplus\GdipSetClipRegion", Ptr, pGraphics, Ptr, Region, "int", CombineMode)
}

Gdip_CreateRegion()
{
	DllCall("gdiplus\GdipCreateRegion", "UInt*", Region)
	return Region
}

Gdip_DeleteRegion(Region)
{
	return DllCall("gdiplus\GdipDeleteRegion", A_PtrSize ? "UPtr" : "UInt", Region)
}

;#####################################################################################
; BitmapLockBits
;#####################################################################################

Gdip_LockBits(pBitmap, x, y, w, h, ByRef Stride, ByRef Scan0, ByRef BitmapData, LockMode = 3, PixelFormat = 0x26200a)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	CreateRect(Rect, x, y, w, h)
	VarSetCapacity(BitmapData, 16+2*(A_PtrSize ? A_PtrSize : 4), 0)
	E := DllCall("Gdiplus\GdipBitmapLockBits", Ptr, pBitmap, Ptr, &Rect, "uint", LockMode, "int", PixelFormat, Ptr, &BitmapData)
	Stride := NumGet(BitmapData, 8, "Int")
	Scan0 := NumGet(BitmapData, 16, Ptr)
	return E
}

;#####################################################################################

Gdip_UnlockBits(pBitmap, ByRef BitmapData)
{
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	return DllCall("Gdiplus\GdipBitmapUnlockBits", Ptr, pBitmap, Ptr, &BitmapData)
}

;#####################################################################################

Gdip_SetLockBitPixel(ARGB, Scan0, x, y, Stride)
{
	Numput(ARGB, Scan0+0, (x*4)+(y*Stride), "UInt")
}

;#####################################################################################

Gdip_GetLockBitPixel(Scan0, x, y, Stride)
{
	return NumGet(Scan0+0, (x*4)+(y*Stride), "UInt")
}

;#####################################################################################

Gdip_PixelateBitmap(pBitmap, ByRef pBitmapOut, BlockSize)
{
	static PixelateBitmap
	
	Ptr := A_PtrSize ? "UPtr" : "UInt"
	
	if (!PixelateBitmap)
	{
		if A_PtrSize != 8 ; x86 machine code
			MCode_PixelateBitmap =
		(LTrim Join
		558BEC83EC3C8B4514538B5D1C99F7FB56578BC88955EC894DD885C90F8E830200008B451099F7FB8365DC008365E000894DC88955F08945E833FF897DD4
		397DE80F8E160100008BCB0FAFCB894DCC33C08945F88945FC89451C8945143BD87E608B45088D50028BC82BCA8BF02BF2418945F48B45E02955F4894DC4
		8D0CB80FAFCB03CA895DD08BD1895DE40FB64416030145140FB60201451C8B45C40FB604100145FC8B45F40FB604020145F883C204FF4DE475D6034D18FF
		4DD075C98B4DCC8B451499F7F98945148B451C99F7F989451C8B45FC99F7F98945FC8B45F899F7F98945F885DB7E648B450C8D50028BC82BCA83C103894D
		C48BC82BCA41894DF48B4DD48945E48B45E02955E48D0C880FAFCB03CA895DD08BD18BF38A45148B7DC48804178A451C8B7DF488028A45FC8804178A45F8
		8B7DE488043A83C2044E75DA034D18FF4DD075CE8B4DCC8B7DD447897DD43B7DE80F8CF2FEFFFF837DF0000F842C01000033C08945F88945FC89451C8945
		148945E43BD87E65837DF0007E578B4DDC034DE48B75E80FAF4D180FAFF38B45088D500203CA8D0CB18BF08BF88945F48B45F02BF22BFA2955F48945CC0F
		B6440E030145140FB60101451C0FB6440F010145FC8B45F40FB604010145F883C104FF4DCC75D8FF45E4395DE47C9B8B4DF00FAFCB85C9740B8B451499F7
		F9894514EB048365140033F63BCE740B8B451C99F7F989451CEB0389751C3BCE740B8B45FC99F7F98945FCEB038975FC3BCE740B8B45F899F7F98945F8EB
		038975F88975E43BDE7E5A837DF0007E4C8B4DDC034DE48B75E80FAF4D180FAFF38B450C8D500203CA8D0CB18BF08BF82BF22BFA2BC28B55F08955CC8A55
		1488540E038A551C88118A55FC88540F018A55F888140183C104FF4DCC75DFFF45E4395DE47CA68B45180145E0015DDCFF4DC80F8594FDFFFF8B451099F7
		FB8955F08945E885C00F8E450100008B45EC0FAFC38365DC008945D48B45E88945CC33C08945F88945FC89451C8945148945103945EC7E6085DB7E518B4D
		D88B45080FAFCB034D108D50020FAF4D18034DDC8BF08BF88945F403CA2BF22BFA2955F4895DC80FB6440E030145140FB60101451C0FB6440F010145FC8B
		45F40FB604080145F883C104FF4DC875D8FF45108B45103B45EC7CA08B4DD485C9740B8B451499F7F9894514EB048365140033F63BCE740B8B451C99F7F9
		89451CEB0389751C3BCE740B8B45FC99F7F98945FCEB038975FC3BCE740B8B45F899F7F98945F8EB038975F88975103975EC7E5585DB7E468B4DD88B450C
		0FAFCB034D108D50020FAF4D18034DDC8BF08BF803CA2BF22BFA2BC2895DC88A551488540E038A551C88118A55FC88540F018A55F888140183C104FF4DC8
		75DFFF45108B45103B45EC7CAB8BC3C1E0020145DCFF4DCC0F85CEFEFFFF8B4DEC33C08945F88945FC89451C8945148945103BC87E6C3945F07E5C8B4DD8
		8B75E80FAFCB034D100FAFF30FAF4D188B45088D500203CA8D0CB18BF08BF88945F48B45F02BF22BFA2955F48945C80FB6440E030145140FB60101451C0F
		B6440F010145FC8B45F40FB604010145F883C104FF4DC875D833C0FF45108B4DEC394D107C940FAF4DF03BC874068B451499F7F933F68945143BCE740B8B
		451C99F7F989451CEB0389751C3BCE740B8B45FC99F7F98945FCEB038975FC3BCE740B8B45F899F7F98945F8EB038975F88975083975EC7E63EB0233F639
		75F07E4F8B4DD88B75E80FAFCB034D080FAFF30FAF4D188B450C8D500203CA8D0CB18BF08BF82BF22BFA2BC28B55F08955108A551488540E038A551C8811
		8A55FC88540F018A55F888140883C104FF4D1075DFFF45088B45083B45EC7C9F5F5E33C05BC9C21800
		)
		else ; x64 machine code
			MCode_PixelateBitmap =
		(LTrim Join
		4489442418488954241048894C24085355565741544155415641574883EC28418BC1448B8C24980000004C8BDA99488BD941F7F9448BD0448BFA8954240C
		448994248800000085C00F8E9D020000418BC04533E4458BF299448924244C8954241041F7F933C9898C24980000008BEA89542404448BE889442408EB05
		4C8B5C24784585ED0F8E1A010000458BF1418BFD48897C2418450FAFF14533D233F633ED4533E44533ED4585C97E5B4C63BC2490000000418D040A410FAF
		C148984C8D441802498BD9498BD04D8BD90FB642010FB64AFF4403E80FB60203E90FB64AFE4883C2044403E003F149FFCB75DE4D03C748FFCB75D0488B7C
		24188B8C24980000004C8B5C2478418BC59941F7FE448BE8418BC49941F7FE448BE08BC59941F7FE8BE88BC69941F7FE8BF04585C97E4048639C24900000
		004103CA4D8BC1410FAFC94863C94A8D541902488BCA498BC144886901448821408869FF408871FE4883C10448FFC875E84803D349FFC875DA8B8C249800
		0000488B5C24704C8B5C24784183C20448FFCF48897C24180F850AFFFFFF8B6C2404448B2424448B6C24084C8B74241085ED0F840A01000033FF33DB4533
		DB4533D24533C04585C97E53488B74247085ED7E42438D0C04418BC50FAF8C2490000000410FAFC18D04814863C8488D5431028BCD0FB642014403D00FB6
		024883C2044403D80FB642FB03D80FB642FA03F848FFC975DE41FFC0453BC17CB28BCD410FAFC985C9740A418BC299F7F98BF0EB0233F685C9740B418BC3
		99F7F9448BD8EB034533DB85C9740A8BC399F7F9448BD0EB034533D285C9740A8BC799F7F9448BC0EB034533C033D24585C97E4D4C8B74247885ED7E3841
		8D0C14418BC50FAF8C2490000000410FAFC18D04814863C84A8D4431028BCD40887001448818448850FF448840FE4883C00448FFC975E8FFC2413BD17CBD
		4C8B7424108B8C2498000000038C2490000000488B5C24704503E149FFCE44892424898C24980000004C897424100F859EFDFFFF448B7C240C448B842480
		000000418BC09941F7F98BE8448BEA89942498000000896C240C85C00F8E3B010000448BAC2488000000418BCF448BF5410FAFC9898C248000000033FF33
		ED33F64533DB4533D24533C04585FF7E524585C97E40418BC5410FAFC14103C00FAF84249000000003C74898488D541802498BD90FB642014403D00FB602
		4883C2044403D80FB642FB03F00FB642FA03E848FFCB75DE488B5C247041FFC0453BC77CAE85C9740B418BC299F7F9448BE0EB034533E485C9740A418BC3
		99F7F98BD8EB0233DB85C9740A8BC699F7F9448BD8EB034533DB85C9740A8BC599F7F9448BD0EB034533D24533C04585FF7E4E488B4C24784585C97E3541
		8BC5410FAFC14103C00FAF84249000000003C74898488D540802498BC144886201881A44885AFF448852FE4883C20448FFC875E941FFC0453BC77CBE8B8C
		2480000000488B5C2470418BC1C1E00203F849FFCE0F85ECFEFFFF448BAC24980000008B6C240C448BA4248800000033FF33DB4533DB4533D24533C04585
		FF7E5A488B7424704585ED7E48418BCC8BC5410FAFC94103C80FAF8C2490000000410FAFC18D04814863C8488D543102418BCD0FB642014403D00FB60248
		83C2044403D80FB642FB03D80FB642FA03F848FFC975DE41FFC0453BC77CAB418BCF410FAFCD85C9740A418BC299F7F98BF0EB0233F685C9740B418BC399
		F7F9448BD8EB034533DB85C9740A8BC399F7F9448BD0EB034533D285C9740A8BC799F7F9448BC0EB034533C033D24585FF7E4E4585ED7E42418BCC8BC541
		0FAFC903CA0FAF8C2490000000410FAFC18D04814863C8488B442478488D440102418BCD40887001448818448850FF448840FE4883C00448FFC975E8FFC2
		413BD77CB233C04883C428415F415E415D415C5F5E5D5BC3
		)
		
		VarSetCapacity(PixelateBitmap, StrLen(MCode_PixelateBitmap)//2)
		Loop % StrLen(MCode_PixelateBitmap)//2		;%
			NumPut("0x" SubStr(MCode_PixelateBitmap, (2*A_Index)-1, 2), PixelateBitmap, A_Index-1, "UChar")
		DllCall("VirtualProtect", Ptr, &PixelateBitmap, Ptr, VarSetCapacity(PixelateBitmap), "uint", 0x40, A_PtrSize ? "UPtr*" : "UInt*", 0)
	}
	
	Gdip_GetImageDimensions(pBitmap, Width, Height)
	
	if (Width != Gdip_GetImageWidth(pBitmapOut) || Height != Gdip_GetImageHeight(pBitmapOut))
		return -1
	if (BlockSize > Width || BlockSize > Height)
		return -2
	
	E1 := Gdip_LockBits(pBitmap, 0, 0, Width, Height, Stride1, Scan01, BitmapData1)
	E2 := Gdip_LockBits(pBitmapOut, 0, 0, Width, Height, Stride2, Scan02, BitmapData2)
	if (E1 || E2)
		return -3
	
	E := DllCall(&PixelateBitmap, Ptr, Scan01, Ptr, Scan02, "int", Width, "int", Height, "int", Stride1, "int", BlockSize)
	
	Gdip_UnlockBits(pBitmap, BitmapData1), Gdip_UnlockBits(pBitmapOut, BitmapData2)
	return 0
}

;#####################################################################################

Gdip_ToARGB(A, R, G, B)
{
	return (A << 24) | (R << 16) | (G << 8) | B
}

;#####################################################################################

Gdip_FromARGB(ARGB, ByRef A, ByRef R, ByRef G, ByRef B)
{
	A := (0xff000000 & ARGB) >> 24
	R := (0x00ff0000 & ARGB) >> 16
	G := (0x0000ff00 & ARGB) >> 8
	B := 0x000000ff & ARGB
}

;#####################################################################################

Gdip_AFromARGB(ARGB)
{
	return (0xff000000 & ARGB) >> 24
}

;#####################################################################################

Gdip_RFromARGB(ARGB)
{
	return (0x00ff0000 & ARGB) >> 16
}

;#####################################################################################

Gdip_GFromARGB(ARGB)
{
	return (0x0000ff00 & ARGB) >> 8
}

;#####################################################################################

Gdip_BFromARGB(ARGB)
{
	return 0x000000ff & ARGB
}

;#####################################################################################

StrGetB(Address, Length=-1, Encoding=0)
{
	; Flexible parameter handling:
	if Length is not integer
		Encoding := Length,  Length := -1
	
	; Check for obvious errors.
	if (Address+0 < 1024)
		return
	
	; Ensure 'Encoding' contains a numeric identifier.
	if Encoding = UTF-16
		Encoding = 1200
	else if Encoding = UTF-8
		Encoding = 65001
	else if SubStr(Encoding,1,2)="CP"
		Encoding := SubStr(Encoding,3)
	
	if !Encoding ; "" or 0
	{
		; No conversion necessary, but we might not want the whole string.
		if (Length == -1)
			Length := DllCall("lstrlen", "uint", Address)
		VarSetCapacity(String, Length)
		DllCall("lstrcpyn", "str", String, "uint", Address, "int", Length + 1)
	}
	else if Encoding = 1200 ; UTF-16
	{
		char_count := DllCall("WideCharToMultiByte", "uint", 0, "uint", 0x400, "uint", Address, "int", Length, "uint", 0, "uint", 0, "uint", 0, "uint", 0)
		VarSetCapacity(String, char_count)
		DllCall("WideCharToMultiByte", "uint", 0, "uint", 0x400, "uint", Address, "int", Length, "str", String, "int", char_count, "uint", 0, "uint", 0)
	}
	else if Encoding is integer
	{
		; Convert from target encoding to UTF-16 then to the active code page.
		char_count := DllCall("MultiByteToWideChar", "uint", Encoding, "uint", 0, "uint", Address, "int", Length, "uint", 0, "int", 0)
		VarSetCapacity(String, char_count * 2)
		char_count := DllCall("MultiByteToWideChar", "uint", Encoding, "uint", 0, "uint", Address, "int", Length, "uint", &String, "int", char_count * 2)
		String := StrGetB(&String, char_count, 1200)
	}
	
	return String
}

/**
	* Lib: JSON.ahk
	*     JSON lib for AutoHotkey.
	* Version:
	*     v2.1.3 [updated 04/18/2016 (MM/DD/YYYY)]
	* License:
	*     WTFPL [http://wtfpl.net/]
	* Requirements:
	*     Latest version of AutoHotkey (v1.1+ or v2.0-a+)
	* Installation:
	*     Use #Include JSON.ahk or copy into a function library folder and then
	*     use #Include <JSON>
	* Links:
	*     GitHub:     - https://github.com/cocobelgica/AutoHotkey-JSON
	*     Forum Topic - http://goo.gl/r0zI8t
	*     Email:      - cocobelgica <at> gmail <dot> com
*/


/**
	* Class: JSON
	*     The JSON object contains methods for parsing JSON and converting values
	*     to JSON. Callable - NO; Instantiable - YES; Subclassable - YES;
	*     Nestable(via #Include) - NO.
	* Methods:
	*     Load() - see relevant documentation before method definition header
	*     Dump() - see relevant documentation before method definition header
*/
class JSON
{
	/**
		* Method: Load
		*     Parses a JSON string into an AHK value
		* Syntax:
		*     value := JSON.Load( text [, reviver ] )
		* Parameter(s):
		*     value      [retval] - parsed value
		*     text    [in, ByRef] - JSON formatted string
		*     reviver   [in, opt] - function object, similar to JavaScript's
		*                           JSON.parse() 'reviver' parameter
	*/
	class Load extends JSON.Functor
	{
		Call(self, ByRef text, reviver:="")
		{
			this.rev := IsObject(reviver) ? reviver : false
		; Object keys(and array indices) are temporarily stored in arrays so that
		; we can enumerate them in the order they appear in the document/text instead
		; of alphabetically. Skip if no reviver function is specified.
			this.keys := this.rev ? {} : false
			
			static quot := Chr(34), bashq := "\" . quot
			     , json_value := quot . "{[01234567890-tfn"
			     , json_value_or_array_closing := quot . "{[]01234567890-tfn"
			     , object_key_or_object_closing := quot . "}"
			
			key := ""
			is_key := false
			root := {}
			stack := [root]
			next := json_value
			pos := 0
			
			while ((ch := SubStr(text, ++pos, 1)) != "") {
				if InStr(" `t`r`n", ch)
					continue
				if !InStr(next, ch, 1)
					this.ParseError(next, text, pos)
				
				holder := stack[1]
				is_array := holder.IsArray
				
				if InStr(",:", ch) {
					next := (is_key := !is_array && ch == ",") ? quot : json_value
					
				} else if InStr("}]", ch) {
					ObjRemoveAt(stack, 1)
					next := stack[1]==root ? "" : stack[1].IsArray ? ",]" : ",}"
					
				} else {
					if InStr("{[", ch) {
					; Check if Array() is overridden and if its return value has
					; the 'IsArray' property. If so, Array() will be called normally,
					; otherwise, use a custom base object for arrays
						static json_array := Func("Array").IsBuiltIn || ![].IsArray ? {IsArray: true} : 0
						
					; sacrifice readability for minor(actually negligible) performance gain
						(ch == "{")
							? ( is_key := true
							  , value := {}
							  , next := object_key_or_object_closing )
						; ch == "["
							: ( value := json_array ? new json_array : []
							  , next := json_value_or_array_closing )
						
						ObjInsertAt(stack, 1, value)
						
						if (this.keys)
							this.keys[value] := []
						
					} else {
						if (ch == quot) {
							i := pos
							while (i := InStr(text, quot,, i+1)) {
								value := StrReplace(SubStr(text, pos+1, i-pos-1), "\\", "\u005c")
								
								static tail := A_AhkVersion<"2" ? 0 : -1
								if (SubStr(value, tail) != "\")
									break
							}
							
							if (!i)
								this.ParseError("'", text, pos)
							
							value := StrReplace(value,  "\/",  "/")
							, value := StrReplace(value, bashq, quot)
							, value := StrReplace(value,  "\b", "`b")
							, value := StrReplace(value,  "\f", "`f")
							, value := StrReplace(value,  "\n", "`n")
							, value := StrReplace(value,  "\r", "`r")
							, value := StrReplace(value,  "\t", "`t")
							
							pos := i ; update pos
							
							i := 0
							while (i := InStr(value, "\",, i+1)) {
								if !(SubStr(value, i+1, 1) == "u")
									this.ParseError("\", text, pos - StrLen(SubStr(value, i+1)))
								
								uffff := Abs("0x" . SubStr(value, i+2, 4))
								if (A_IsUnicode || uffff < 0x100)
									value := SubStr(value, 1, i-1) . Chr(uffff) . SubStr(value, i+6)
							}
							
							if (is_key) {
								key := value, next := ":"
								continue
							}
							
						} else {
							value := SubStr(text, pos, i := RegExMatch(text, "[\]\},\s]|$",, pos)-pos)
							
							static number := "number", integer :="integer"
							if value is %number%
							{
								if value is %integer%
									value += 0
							}
							else if (value == "true" || value == "false")
								value := %value% + 0
							else if (value == "null")
								value := ""
							else
							; we can do more here to pinpoint the actual culprit
							; but that's just too much extra work.
								this.ParseError(next, text, pos, i)
							
							pos += i-1
						}
						
						next := holder==root ? "" : is_array ? ",]" : ",}"
					} ; If InStr("{[", ch) { ... } else
					
					is_array? key := ObjPush(holder, value) : holder[key] := value
					
					if (this.keys && this.keys.HasKey(holder))
						this.keys[holder].Push(key)
				}
				
			} ; while ( ... )
			
			return this.rev ? this.Walk(root, "") : root[""]
		}
		
		ParseError(expect, ByRef text, pos, len:=1)
		{
			static quot := Chr(34), qurly := quot . "}"
			
			line := StrSplit(SubStr(text, 1, pos), "`n", "`r").Length()
			col := pos - InStr(text, "`n",, -(StrLen(text)-pos+1))
			msg := Format("{1}`n`nLine:`t{2}`nCol:`t{3}`nChar:`t{4}"
			,     (expect == "")     ? "Extra data"
			    : (expect == "'")    ? "Unterminated string starting at"
			    : (expect == "\")    ? "Invalid \escape"
			    : (expect == ":")    ? "Expecting ':' delimiter"
			    : (expect == quot)   ? "Expecting object key enclosed in double quotes"
			    : (expect == qurly)  ? "Expecting object key enclosed in double quotes or object closing '}'"
			    : (expect == ",}")   ? "Expecting ',' delimiter or object closing '}'"
			    : (expect == ",]")   ? "Expecting ',' delimiter or array closing ']'"
			    : InStr(expect, "]") ? "Expecting JSON value or array closing ']'"
			    :                      "Expecting JSON value(string, number, true, false, null, object or array)"
			, line, col, pos)
			
			static offset := A_AhkVersion<"2" ? -3 : -4
			;throw Exception(msg, offset, SubStr(text, pos, len))
		}
		
		Walk(holder, key)
		{
			value := holder[key]
			if IsObject(value) {
				for i, k in this.keys[value] {
					; check if ObjHasKey(value, k) ??
					v := this.Walk(value, k)
					if (v != JSON.Undefined)
						value[k] := v
					else
						ObjDelete(value, k)
				}
			}
			
			return this.rev.Call(holder, key, value)
		}
	}
	
	/**
		* Method: Dump
		*     Converts an AHK value into a JSON string
		* Syntax:
		*     str := JSON.Dump( value [, replacer, space ] )
		* Parameter(s):
		*     str        [retval] - JSON representation of an AHK value
		*     value          [in] - any value(object, string, number)
		*     replacer  [in, opt] - function object, similar to JavaScript's
		*                           JSON.stringify() 'replacer' parameter
		*     space     [in, opt] - similar to JavaScript's JSON.stringify()
		*                           'space' parameter
	*/
	class Dump extends JSON.Functor
	{
		Call(self, value, replacer:="", space:="")
		{
			this.rep := IsObject(replacer) ? replacer : ""
			
			this.gap := ""
			if (space) {
				static integer := "integer"
				if space is %integer%
					Loop, % ((n := Abs(space))>10 ? 10 : n)
						this.gap .= " "
				else
					this.gap := SubStr(space, 1, 10)
				
				this.indent := "`n"
			}
			
			return this.Str({"": value}, "")
		}
		
		Str(holder, key)
		{
			value := holder[key]
			
			if (this.rep)
				value := this.rep.Call(holder, key, ObjHasKey(holder, key) ? value : JSON.Undefined)
			
			if IsObject(value) {
			; Check object type, skip serialization for other object types such as
			; ComObject, Func, BoundFunc, FileObject, RegExMatchObject, Property, etc.
				static type := A_AhkVersion<"2" ? "" : Func("Type")
				if (type ? type.Call(value) == "Object" : ObjGetCapacity(value) != "") {
					if (this.gap) {
						stepback := this.indent
						this.indent .= this.gap
					}
					
					is_array := value.IsArray
				; Array() is not overridden, rollback to old method of
				; identifying array-like objects. Due to the use of a for-loop
				; sparse arrays such as '[1,,3]' are detected as objects({}). 
					if (!is_array) {
						for i in value
							is_array := i == A_Index
						until !is_array
					}
					
					str := ""
					if (is_array) {
						Loop, % value.Length() {
							if (this.gap)
								str .= this.indent
							
							v := this.Str(value, A_Index)
							str .= (v != "") ? v . "," : "null,"
						}
					} else {
						colon := this.gap ? ": " : ":"
						for k in value {
							v := this.Str(value, k)
							if (v != "") {
								if (this.gap)
									str .= this.indent
								
								str .= this.Quote(k) . colon . v . ","
							}
						}
					}
					
					if (str != "") {
						str := RTrim(str, ",")
						if (this.gap)
							str .= stepback
					}
					
					if (this.gap)
						this.indent := stepback
					
					return is_array ? "[" . str . "]" : "{" . str . "}"
				}
				
			} else ; is_number ? value : "value"
				return ObjGetCapacity([value], 1)=="" ? value : this.Quote(value)
		}
		
		Quote(string)
		{
			static quot := Chr(34), bashq := "\" . quot
			
			if (string != "") {
				string := StrReplace(string,  "\",  "\\")
				; , string := StrReplace(string,  "/",  "\/") ; optional in ECMAScript
				, string := StrReplace(string, quot, bashq)
				, string := StrReplace(string, "`b",  "\b")
				, string := StrReplace(string, "`f",  "\f")
				, string := StrReplace(string, "`n",  "\n")
				, string := StrReplace(string, "`r",  "\r")
				, string := StrReplace(string, "`t",  "\t")
				
				static rx_escapable := A_AhkVersion<"2" ? "O)[^\x20-\x7e]" : "[^\x20-\x7e]"
				while RegExMatch(string, rx_escapable, m)
					string := StrReplace(string, m.Value, Format("\u{1:04x}", Ord(m.Value)))
			}
			
			return quot . string . quot
		}
	}
	
	/**
		* Property: Undefined
		*     Proxy for 'undefined' type
		* Syntax:
		*     undefined := JSON.Undefined
		* Remarks:
		*     For use with reviver and replacer functions since AutoHotkey does not
		*     have an 'undefined' type. Returning blank("") or 0 won't work since these
		*     can't be distnguished from actual JSON values. This leaves us with objects.
		*     Replacer() - the caller may return a non-serializable AHK objects such as
		*     ComObject, Func, BoundFunc, FileObject, RegExMatchObject, and Property to
		*     mimic the behavior of returning 'undefined' in JavaScript but for the sake
		*     of code readability and convenience, it's better to do 'return JSON.Undefined'.
		*     Internally, the property returns a ComObject with the variant type of VT_EMPTY.
	*/
	Undefined[]
	{
		get {
			static empty := {}, vt_empty := ComObject(0, &empty, 1)
			return vt_empty
		}
	}
	
	class Functor
	{
		__Call(method, ByRef arg, args*)
		{
		; When casting to Call(), use a new instance of the "function object"
		; so as to avoid directly storing the properties(used across sub-methods)
		; into the "function object" itself.
			if IsObject(method)
				return (new this).Call(method, arg, args*)
			else if (method == "")
				return (new this).Call(arg, args*)
		}
	}
}

/*
	A basic memory class by RHCP:
	
	This is a wrapper for commonly used read and write memory functions.
	It also contains a variety of pattern scan functions.
	This class allows scripts to read/write integers and strings of various types. 
	Pointer addresses can easily be read/written by passing the base address and offsets to the various read/write functions.
	
	Process handles are kept open between reads. This increases speed.
	However, if a program closes/restarts then the process handle will become invalid
    and you will need to re-open another handle (blank/destroy the object and recreate it)
	isHandleValid() can be used to check if a handle is still active/valid.
	
	read(), readString(), write(), and writeString() can be used to read and write memory addresses respectively.
	
	readRaw() can be used to dump large chunks of memory, this is considerably faster when
	reading data from a large structure compared to repeated calls to read(). 
	For example, reading a single UInt takes approximately the same amount of time as reading 1000 bytes via readRaw().
		Although, most people wouldn't notice the performance difference. This does however require you 
	to retrieve the values using AHK's numget()/strGet() from the dumped memory.
	
	In a similar fashion writeRaw() allows a buffer to be be written in a single operation. 
	
	When the new operator is used this class returns an object which can be used to read that process's 
	memory space.To read another process simply create another object.
	
	Process handles are automatically closed when the script exits/restarts or when you free the object.
	
	**Notes:
	This was initially written for 32 bit target processes, however the various read/write functions
	should now completely support pointers in 64 bit target applications. The only caveat is that the AHK exe must also be 64 bit.  
	If AHK is 32 bit and the target application is 64 bit you can still read, write, and use pointers, so long as the addresses
		fit inside a 4 byte pointer, i.e. The maximum address is limited to the 32 bit range.
	
	The various pattern scan functions are intended to be used on 32 bit target applications, however: 
	- A 32 bit AHK script can perform pattern scans on a 32 bit target application.
	- A 32 bit AHK script may be able to perform pattern scans on a 64 bit process, providing the addresses fall within the 32 bit range.             
	- A 64 bit AHK script should be able to perform pattern scans on a 32 or 64 bit target application without issue. 
	
	If the target process has admin privileges, then the AHK script will also require admin privileges. 
		
	AHK doesn't support unsigned 64bit ints, you can however read them as Int64 and interpret negative values as large numbers.       
	
	
	Commonly used methods:
	read()
	readString()
	readRaw()
	write()
	writeString()
	writeBytes()
	writeRaw()
	isHandleValid() 
	getModuleBaseAddress()
	
	Less commonly used methods:
	getProcessBaseAddress()
	hexStringToPattern()
	stringToPattern()    
	modulePatternScan()
	processPatternScan()
	addressPatternScan()
	rawPatternScan()
	getModules()
	numberOfBytesRead()
	numberOfBytesWritten()
	suspend()
	resume()
	
	Internal methods: (some may be useful when directly called)
	getAddressFromOffsets() ; This will return the final memory address of a pointer. This is useful if the pointed address only changes on startup or map/level change and you want to eliminate the overhead associated with pointers.
	isTargetProcess64Bit()
	pointer() 
	GetModuleFileNameEx()
	EnumProcessModulesEx()
	GetModuleInformation()
	getNeedleFromAOBPattern()
	virtualQueryEx()
	patternScan()
	bufferScanForMaskedPattern()
	openProcess()
	closeHandle() 
	
	Useful properties:  (Do not modify the values of these properties - they are set automatically)
	baseAddress             ; The base address of the target process
	hProcess                ; The handle to the target process
	PID                     ; The PID of the target process
	currentProgram          ; The string the user used to identify the target process e.g. "ahk_exe calc.exe" 
	isTarget64bit           ; True if target process is 64 bit, otherwise false
	readStringLastError     ; Used to check for success/failure when reading a string
	
     Useful editable properties:
	insertNullTerminator    ; Determines if a null terminator is inserted when writing strings.
	
	
	Usage:
	
        ; **Note: If you wish to try this calc example, consider using the 32 bit version of calc.exe - 
        ;         which is in C:\Windows\SysWOW64\calc.exe on win7 64 bit systems.
	
        ; The contents of this file can be copied directly into your script. Alternately, you can copy the classMemory.ahk file into your library folder,
        ; in which case you will need to use the #include directive in your script i.e. 
	#Include <classMemory>
	
        ; You can use this code to check if you have installed the class correctly.
	if (_ClassMemory.__Class != "_ClassMemory")
	{
		msgbox class memory not correctly installed. Or the (global class) variable "_ClassMemory" has been overwritten
		ExitApp
	}
	
        ; Open a process with sufficient access to read and write memory addresses (this is required before you can use the other functions)
        ; You only need to do this once. But if the process closes/restarts, then you will need to perform this step again. Refer to the notes section below.
        ; Also, if the target process is running as admin, then the script will also require admin rights!
        ; Note: The program identifier can be any AHK windowTitle i.e.ahk_exe, ahk_class, ahk_pid, or simply the window title.
        ; hProcessCopy is an optional variable in which the opened handled is stored. 
	
	calc := new _ClassMemory("ahk_exe calc.exe", "", hProcessCopy) 
	
        ; Check if the above method was successful.
	if !isObject(calc) 
	{
		msgbox failed to open a handle
		if (hProcessCopy = 0)
			msgbox The program isn't running (not found) or you passed an incorrect program identifier parameter. In some cases _ClassMemory.setSeDebugPrivilege() may be required. 
		else if (hProcessCopy = "")
			msgbox OpenProcess failed. If the target process has admin rights, then the script also needs to be ran as admin. _ClassMemory.setSeDebugPrivilege() may also be required. Consult A_LastError for more information.
		ExitApp
	}
	
        ; Get the process's base address.
        ; When using the new operator this property is automatically set to the result of getModuleBaseAddress() or getProcessBaseAddress();
        ; the specific method used depends on the bitness of the target application and AHK.
        ; If the returned address is incorrect and the target application is 64 bit, but AHK is 32 bit, try using the 64 bit version of AHK.
	msgbox % calc.BaseAddress 
	
        ; Get the base address of a specific module.
	msgbox % calc.getModuleBaseAddress("GDI32.dll")
	
        ; The rest of these examples are just for illustration (the addresses specified are probably not valid).
        ; You can use cheat engine to find real addresses to read and write for testing purposes.
	
        ; Write 1234 as a UInt at address 0x0016CB60.
	calc.write(0x0016CB60, 1234, "UInt")
	
        ; Read a UInt.
	value := calc.read(0x0016CB60, "UInt")
	
        ; Read a pointer with offsets 0x20 and 0x15C which points to a UChar. 
	value := calc.read(pointerBase, "UChar", 0x20, 0x15C)
	
        ; Note: read(), readString(), readRaw(), write(), writeString(), and writeRaw() all support pointers/offsets.
        ; An array of pointers can be passed directly, i.e.
	arrayPointerOffsets := [0x20, 0x15C]
	value := calc.read(pointerBase, "UChar", arrayPointerOffsets*)
        ; Or they can be entered manually.
	value := calc.read(pointerBase, "UChar", 0x20, 0x15C)
        ; You can also pass all the parameters directly, i.e.
	aMyPointer := [pointerBase, "UChar", 0x20, 0x15C]
	value := calc.read(aMyPointer*)
	
	
        ; Read a utf-16 null terminated string of unknown size at address 0x1234556 - the function will read until the null terminator is found or something goes wrong.
	string := calc.readString(0x1234556, length := 0, encoding := "utf-16")
	
        ; Read a utf-8 encoded string which is 12 bytes long at address 0x1234556.
	string := calc.readString(0x1234556, 12)
	
        ; By default a null terminator is included at the end of written strings for writeString().
        ; The nullterminator property can be used to change this.
	_ClassMemory.insertNullTerminator := False ; This will change the property for all processes
	calc.insertNullTerminator := False ; Changes the property for just this process     
	
	
	Notes: 
	If the target process exits and then starts again (or restarts) you will need to free the derived object and then use the new operator to create a new object i.e. 
		calc := [] ; or calc := "" ; free the object. This is actually optional if using the line below, as the line below would free the previous derived object calc prior to initialising the new copy.
	calc := new _ClassMemory("ahk_exe calc.exe") ; Create a new derived object to read calc's memory.
	isHandleValid() can be used to check if a target process has closed or restarted.
*/

class _ClassMemory
{
    ; List of useful accessible values. Some of these inherited values (the non objects) are set when the new operator is used.
	static baseAddress, hProcess, PID, currentProgram
    , insertNullTerminator := True
    , readStringLastError := False
    , isTarget64bit := False
    , ptrType := "UInt"
    , aTypeSize := {    "UChar":    1,  "Char":     1
                    ,   "UShort":   2,  "Short":    2
                    ,   "UInt":     4,  "Int":      4
                    ,   "UFloat":   4,  "Float":    4
                    ,   "Int64":    8,  "Double":   8}  
    , aRights := {  "PROCESS_ALL_ACCESS": 0x001F0FFF
                ,   "PROCESS_CREATE_PROCESS": 0x0080
                ,   "PROCESS_CREATE_THREAD": 0x0002
                ,   "PROCESS_DUP_HANDLE": 0x0040
                ,   "PROCESS_QUERY_INFORMATION": 0x0400
                ,   "PROCESS_QUERY_LIMITED_INFORMATION": 0x1000
                ,   "PROCESS_SET_INFORMATION": 0x0200
                ,   "PROCESS_SET_QUOTA": 0x0100
                ,   "PROCESS_SUSPEND_RESUME": 0x0800
                ,   "PROCESS_TERMINATE": 0x0001
                ,   "PROCESS_VM_OPERATION": 0x0008
                ,   "PROCESS_VM_READ": 0x0010
                ,   "PROCESS_VM_WRITE": 0x0020
                ,   "SYNCHRONIZE": 0x00100000}
	
	
    ; Method:    __new(program, dwDesiredAccess := "", byRef handle := "", windowMatchMode := 3)
    ; Example:  derivedObject := new _ClassMemory("ahk_exe calc.exe")
    ;           This is the first method which should be called when trying to access a program's memory. 
    ;           If the process is successfully opened, an object is returned which can be used to read that processes memory space.
    ;           [derivedObject].hProcess stores the opened handle.
    ;           If the target process closes and re-opens, simply free the derived object and use the new operator again to open a new handle.
    ; Parameters:
    ;   program             The program to be opened. This can be any AHK windowTitle identifier, such as 
    ;                       ahk_exe, ahk_class, ahk_pid, or simply the window title. e.g. "ahk_exe calc.exe" or "Calculator".
    ;                       It's safer not to use the window title, as some things can have the same window title e.g. an open folder called "Starcraft II"
    ;                       would have the same window title as the game itself.
    ;                       *'DetectHiddenWindows, On' is required for hidden windows*
    ;   dwDesiredAccess     The access rights requested when opening the process.
    ;                       If this parameter is null the process will be opened with the following rights
    ;                       PROCESS_QUERY_INFORMATION, PROCESS_VM_OPERATION, PROCESS_VM_READ, PROCESS_VM_WRITE, & SYNCHRONIZE
    ;                       This access level is sufficient to allow all of the methods in this class to work.
    ;                       Specific process access rights are listed here http://msdn.microsoft.com/en-us/library/windows/desktop/ms684880(v=vs.85).aspx                           
    ;   handle (Output)     Optional variable in which a copy of the opened processes handle will be stored.
    ;                       Values:
    ;                           Null    OpenProcess failed. The script may need to be run with admin rights admin, 
    ;                                   and/or with the use of _ClassMemory.setSeDebugPrivilege(). Consult A_LastError for more information.
    ;                           0       The program isn't running (not found) or you passed an incorrect program identifier parameter.
    ;                                   In some cases _ClassMemory.setSeDebugPrivilege() may be required.
    ;                           Positive Integer    A handle to the process. (Success)
    ;   windowMatchMode -   Determines the matching mode used when finding the program (windowTitle).
    ;                       The default value is 3 i.e. an exact match. Refer to AHK's setTitleMathMode for more information.
    ; Return Values: 
    ;   Object  On success an object is returned which can be used to read the processes memory.
    ;   Null    Failure. A_LastError and the optional handle parameter can be consulted for more information. 
	
	
	__new(program, dwDesiredAccess := "", byRef handle := "", windowMatchMode := 3)
	{         
		if this.PID := handle := this.findPID(program, windowMatchMode) ; set handle to 0 if program not found
		{
            ; This default access level is sufficient to read and write memory addresses, and to perform pattern scans.
            ; if the program is run using admin privileges, then this script will also need admin privileges
			if dwDesiredAccess is not integer       
				dwDesiredAccess := this.aRights.PROCESS_QUERY_INFORMATION | this.aRights.PROCESS_VM_OPERATION | this.aRights.PROCESS_VM_READ | this.aRights.PROCESS_VM_WRITE
			dwDesiredAccess |= this.aRights.SYNCHRONIZE ; add SYNCHRONIZE to all handles to allow isHandleValid() to work
			
			if this.hProcess := handle := this.OpenProcess(this.PID, dwDesiredAccess) ; NULL/Blank if failed to open process for some reason
			{
				this.pNumberOfBytesRead := DllCall("GlobalAlloc", "UInt", 0x0040, "Ptr", A_PtrSize, "Ptr") ; 0x0040 initialise to 0
				this.pNumberOfBytesWritten := DllCall("GlobalAlloc", "UInt", 0x0040, "Ptr", A_PtrSize, "Ptr") ; initialise to 0
				
				this.readStringLastError := False
				this.currentProgram := program
				if this.isTarget64bit := this.isTargetProcess64Bit(this.PID, this.hProcess, dwDesiredAccess)
					this.ptrType := "Int64"
				else this.ptrType := "UInt" ; If false or Null (fails) assume 32bit
					
                ; if script is 64 bit, getModuleBaseAddress() should always work
                ; if target app is truly 32 bit, then getModuleBaseAddress()
                ; will work when script is 32 bit
				if (A_PtrSize != 4 || !this.isTarget64bit)
					this.BaseAddress := this.getModuleBaseAddress()
				
                ; If the above failed or wasn't called, fall back to alternate method    
				if this.BaseAddress < 0 || !this.BaseAddress
					this.BaseAddress := this.getProcessBaseAddress(program, windowMatchMode)            
				
				return this
			}
		}
		return
	}
	
	__delete()
	{
		this.closeHandle(this.hProcess)
		if this.pNumberOfBytesRead
			DllCall("GlobalFree", "Ptr", this.pNumberOfBytesRead)
		if this.pNumberOfBytesWritten
			DllCall("GlobalFree", "Ptr", this.pNumberOfBytesWritten)
		return
	}
	
	version()
	{
		return 2.92
	}   
	
	findPID(program, windowMatchMode := "3")
	{
        ; If user passes an AHK_PID, don't bother searching. There are cases where searching windows for PIDs 
        ; wont work - console apps
		if RegExMatch(program, "i)\s*AHK_PID\s+(0x[[:xdigit:]]+|\d+)", pid)
			return pid1
		if windowMatchMode
		{
            ; This is a string and will not contain the 0x prefix
			mode := A_TitleMatchMode
            ; remove hex prefix as SetTitleMatchMode will throw a run time error. This will occur if integer mode is set to hex and user passed an int (unquoted)
			StringReplace, windowMatchMode, windowMatchMode, 0x 
			SetTitleMatchMode, %windowMatchMode%
		}
		WinGet, pid, pid, %program%
		if windowMatchMode
			SetTitleMatchMode, %mode%    ; In case executed in autoexec
		
        ; If use 'ahk_exe test.exe' and winget fails (which can happen when setSeDebugPrivilege is required),
        ; try using the process command. When it fails due to setSeDebugPrivilege, setSeDebugPrivilege will still be required to openProcess
        ; This should also work for apps without windows.
		if (!pid && RegExMatch(program, "i)\bAHK_EXE\b\s*(.*)", fileName))
		{
            ; remove any trailing AHK_XXX arguments
			filename := RegExReplace(filename1, "i)\bahk_(class|id|pid|group)\b.*", "")
			filename := trim(filename)    ; extra spaces will make process command fail       
            ; AHK_EXE can be the full path, so just get filename
			SplitPath, fileName , fileName
			if (fileName) ; if filename blank, scripts own pid is returned
			{
				process, Exist, %fileName%
				pid := ErrorLevel
			}
		}
		
		return pid ? pid : 0 ; PID is null on fail, return 0
	}
    ; Method:   isHandleValid()
    ;           This method provides a means to check if the internal process handle is still valid
    ;           or in other words, the specific target application instance (which you have been reading from)
    ;           has closed or restarted.
    ;           For example, if the target application closes or restarts the handle will become invalid
    ;           and subsequent calls to this method will return false.
    ;
    ; Return Values: 
    ;   True    The handle is valid.
    ;   False   The handle is not valid. 
    ;
    ; Notes: 
    ;   This operation requires a handle with SYNCHRONIZE access rights.
    ;   All handles, even user specified ones are opened with the SYNCHRONIZE access right.
	
	isHandleValid()
	{
		return 0x102 = DllCall("WaitForSingleObject", "Ptr", this.hProcess, "UInt", 0)
        ; WaitForSingleObject return values
        ; -1 if called with null hProcess (sets lastError to 6 - invalid handle)
        ; 258 / 0x102 WAIT_TIMEOUT - if handle is valid (process still running)
        ; 0  WAIT_OBJECT_0 - if process has terminated        
	}
	
    ; Method:   openProcess(PID, dwDesiredAccess)
    ;           ***Note:    This is an internal method which shouldn't be called directly unless you absolutely know what you are doing.
    ;                       This is because the new operator, in addition to calling this method also sets other values
    ;                       which are required for the other methods to work correctly. 
    ; Parameters:
    ;   PID                 The Process ID of the target process.  
    ;   dwDesiredAccess     The access rights requested when opening the process.
    ;                       Specific process access rights are listed here http://msdn.microsoft.com/en-us/library/windows/desktop/ms684880(v=vs.85).aspx                           
    ; Return Values: 
    ;   Null/blank          OpenProcess failed. If the target process has admin rights, then the script also needs to be ran as admin. 
    ;                       _ClassMemory.setSeDebugPrivilege() may also be required.
    ;   Positive integer    A handle to the process.
	
	openProcess(PID, dwDesiredAccess)
	{
		r := DllCall("OpenProcess", "UInt", dwDesiredAccess, "Int", False, "UInt", PID, "Ptr")
        ; if it fails with 0x5 ERROR_ACCESS_DENIED, try enabling privilege ... lots of users never try this.
        ; there may be other errors which also require DebugPrivilege....
		if (!r && A_LastError = 5)
		{
			this.setSeDebugPrivilege(true) ; no harm in enabling it if it is already enabled by user
			if (r2 := DllCall("OpenProcess", "UInt", dwDesiredAccess, "Int", False, "UInt", PID, "Ptr"))
				return r2
			DllCall("SetLastError", "UInt", 5) ; restore original error if it doesnt work
		}
        ; If fails with 0x5 ERROR_ACCESS_DENIED (when setSeDebugPrivilege() is req.), the func. returns 0 rather than null!! Set it to null.
        ; If fails for another reason, then it is null.
		return r ? r : ""
	}   
	
    ; Method:   closeHandle(hProcess)
    ;           Note:   This is an internal method which is automatically called when the script exits or the derived object is freed/destroyed.
    ;                   There is no need to call this method directly. If you wish to close the handle simply free the derived object. 
    ;                   i.e. derivedObject := [] ; or derivedObject := ""
    ; Parameters:
    ;   hProcess        The handle to the process, as returned by openProcess().
    ; Return Values: 
    ;   Non-Zero        Success
    ;   0               Failure
	
	closeHandle(hProcess)
	{
		return DllCall("CloseHandle", "Ptr", hProcess)
	}
	
    ; Methods:      numberOfBytesRead() / numberOfBytesWritten()
    ;               Returns the number of bytes read or written by the last ReadProcessMemory or WriteProcessMemory operation. 
    ;             
    ; Return Values: 
    ;   zero or positive value      Number of bytes read/written
    ;   -1                          Failure. Shouldn't occur 
	
	numberOfBytesRead()
	{
		return !this.pNumberOfBytesRead ? -1 : NumGet(this.pNumberOfBytesRead+0, "Ptr")
	}
	numberOfBytesWritten()
	{
		return !this.pNumberOfBytesWritten ? -1 : NumGet(this.pNumberOfBytesWritten+0, "Ptr")
	}
	
	
    ; Method:   read(address, type := "UInt", aOffsets*)
    ;           Reads various integer type values
    ; Parameters:
    ;       address -   The memory address of the value or if using the offset parameter, 
    ;                   the base address of the pointer.
    ;       type    -   The integer type. 
    ;                   Valid types are UChar, Char, UShort, Short, UInt, Int, Float, Int64 and Double. 
    ;                   Note: Types must not contain spaces i.e. " UInt" or "UInt " will not work. 
    ;                   When an invalid type is passed the method returns NULL and sets ErrorLevel to -2
    ;       aOffsets* - A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which holds the integer.  
    ; Return Values:
    ;       integer -   Indicates success.
    ;       Null    -   Indicates failure. Check ErrorLevel and A_LastError for more information.
    ;       Note:       Since the returned integer value may be 0, to check for success/failure compare the result
    ;                   against null i.e. if (result = "") then an error has occurred.
    ;                   When reading doubles, adjusting "SetFormat, float, totalWidth.DecimalPlaces"
    ;                   may be required depending on your requirements.
	
	read(address, type := "UInt", aOffsets*)
	{
        ; If invalid type RPM() returns success (as bytes to read resolves to null in dllCall())
        ; so set errorlevel to invalid parameter for DLLCall() i.e. -2
		if !this.aTypeSize.hasKey(type)
			return "", ErrorLevel := -2 
		if DllCall("ReadProcessMemory", "Ptr", this.hProcess, "Ptr", aOffsets.maxIndex() ? this.getAddressFromOffsets(address, aOffsets*) : address, type "*", result, "Ptr", this.aTypeSize[type], "Ptr", this.pNumberOfBytesRead)
			return result
		return        
	}
	
    ; Method:   readRaw(address, byRef buffer, bytes := 4, aOffsets*)
    ;           Reads an area of the processes memory and stores it in the buffer variable
    ; Parameters:
    ;       address  -  The memory address of the area to read or if using the offsets parameter
    ;                   the base address of the pointer which points to the memory region.
    ;       buffer   -  The unquoted variable name for the buffer. This variable will receive the contents from the address space.
    ;                   This method calls varsetCapcity() to ensure the variable has an adequate size to perform the operation. 
    ;                   If the variable already has a larger capacity (from a previous call to varsetcapcity()), then it will not be shrunk. 
    ;                   Therefore it is the callers responsibility to ensure that any subsequent actions performed on the buffer variable
    ;                   do not exceed the bytes which have been read - as these remaining bytes could contain anything.
    ;       bytes   -   The number of bytes to be read.          
    ;       aOffsets* - A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which is to be read
    ; Return Values:
    ;       Non Zero -   Indicates success.
    ;       Zero     -   Indicates failure. Check errorLevel and A_LastError for more information
    ; 
    ; Notes:            The contents of the buffer may then be retrieved using AHK's NumGet() and StrGet() functions.           
    ;                   This method offers significant (~30% and up) performance boost when reading large areas of memory. 
    ;                   As calling ReadProcessMemory for four bytes takes a similar amount of time as it does for 1,000 bytes.                
	
	readRaw(address, byRef buffer, bytes := 4, aOffsets*)
	{
		VarSetCapacity(buffer, bytes)
		return DllCall("ReadProcessMemory", "Ptr", this.hProcess, "Ptr", aOffsets.maxIndex() ? this.getAddressFromOffsets(address, aOffsets*) : address, "Ptr", &buffer, "Ptr", bytes, "Ptr", this.pNumberOfBytesRead)
	}
	
    ; Method:   readString(address, sizeBytes := 0, encoding := "utf-8", aOffsets*)
    ;           Reads string values of various encoding types
    ; Parameters:
    ;       address -   The memory address of the value or if using the offset parameter, 
    ;                   the base address of the pointer.
    ;       sizeBytes - The size (in bytes) of the string to be read.
    ;                   If zero is passed, then the function will read each character until a null terminator is found
    ;                   and then returns the entire string.
    ;       encoding -  This refers to how the string is stored in the program's memory.
    ;                   UTF-8 and UTF-16 are common. Refer to the AHK manual for other encoding types.
    ;       aOffsets* - A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which holds the string.                             
    ;                   
    ;  Return Values:
    ;       String -    On failure an empty (null) string is always returned. Since it's possible for the actual string 
    ;                   being read to be null (empty), then a null return value should not be used to determine failure of the method.
    ;                   Instead the property [derivedObject].ReadStringLastError can be used to check for success/failure.
    ;                   This property is set to 0 on success and 1 on failure. On failure ErrorLevel and A_LastError should be consulted
    ;                   for more information.
    ; Notes:
    ;       For best performance use the sizeBytes parameter to specify the exact size of the string. 
    ;       If the exact size is not known and the string is null terminated, then specifying the maximum
    ;       possible size of the string will yield the same performance.  
    ;       If neither the actual or maximum size is known and the string is null terminated, then specifying
    ;       zero for the sizeBytes parameter is fine. Generally speaking for all intents and purposes the performance difference is
    ;       inconsequential.  
	
	readString(address, sizeBytes := 0, encoding := "UTF-8", aOffsets*)
	{
		bufferSize := VarSetCapacity(buffer, sizeBytes ? sizeBytes : 100, 0)
		this.ReadStringLastError := False
		if aOffsets.maxIndex()
			address := this.getAddressFromOffsets(address, aOffsets*)
		if !sizeBytes  ; read until null terminator is found or something goes wrong
		{
            ; Even if there are multi-byte-characters (bigger than the encodingSize i.e. surrogates) in the string, when reading in encodingSize byte chunks they will never register as null (as they will have bits set on those bytes)
			if (encoding = "utf-16" || encoding = "cp1200")
				encodingSize := 2, charType := "UShort", loopCount := 2
			else encodingSize := 1, charType := "Char", loopCount := 4
				Loop
				{   ; Lets save a few reads by reading in 4 byte chunks
					if !DllCall("ReadProcessMemory", "Ptr", this.hProcess, "Ptr", address + ((outterIndex := A_index) - 1) * 4, "Ptr", &buffer, "Ptr", 4, "Ptr", this.pNumberOfBytesRead) || ErrorLevel
						return "", this.ReadStringLastError := True 
					else loop, %loopCount%
					{
						if NumGet(buffer, (A_Index - 1) * encodingSize, charType) = 0 ; NULL terminator
						{
							if (bufferSize < sizeBytes := outterIndex * 4 - (4 - A_Index * encodingSize)) 
								VarSetCapacity(buffer, sizeBytes)
							break, 2
						}  
					} 
				}
		}
		if DllCall("ReadProcessMemory", "Ptr", this.hProcess, "Ptr", address, "Ptr", &buffer, "Ptr", sizeBytes, "Ptr", this.pNumberOfBytesRead)   
			return StrGet(&buffer,, encoding)  
		return "", this.ReadStringLastError := True             
	}
	
    ; Method:  writeString(address, string, encoding := "utf-8", aOffsets*)
    ;          Encodes and then writes a string to the process.
    ; Parameters:
    ;       address -   The memory address to which data will be written or if using the offset parameter, 
    ;                   the base address of the pointer.
    ;       string -    The string to be written.
    ;       encoding -  This refers to how the string is to be stored in the program's memory.
    ;                   UTF-8 and UTF-16 are common. Refer to the AHK manual for other encoding types.
    ;       aOffsets* - A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which is to be written to.
    ; Return Values:
    ;       Non Zero -   Indicates success.
    ;       Zero     -   Indicates failure. Check errorLevel and A_LastError for more information
    ; Notes:
    ;       By default a null terminator is included at the end of written strings. 
    ;       This behaviour is determined by the property [derivedObject].insertNullTerminator
    ;       If this property is true, then a null terminator will be included.       
	
	writeString(address, string, encoding := "utf-8", aOffsets*)
	{
		encodingSize := (encoding = "utf-16" || encoding = "cp1200") ? 2 : 1
		requiredSize := StrPut(string, encoding) * encodingSize - (this.insertNullTerminator ? 0 : encodingSize)
		VarSetCapacity(buffer, requiredSize)
		StrPut(string, &buffer, StrLen(string) + (this.insertNullTerminator ?  1 : 0), encoding)
		return DllCall("WriteProcessMemory", "Ptr", this.hProcess, "Ptr", aOffsets.maxIndex() ? this.getAddressFromOffsets(address, aOffsets*) : address, "Ptr", &buffer, "Ptr", requiredSize, "Ptr", this.pNumberOfBytesWritten)
	}
	
    ; Method:   write(address, value, type := "Uint", aOffsets*)
    ;           Writes various integer type values to the process.
    ; Parameters:
    ;       address -   The memory address to which data will be written or if using the offset parameter, 
    ;                   the base address of the pointer.
    ;       type    -   The integer type. 
    ;                   Valid types are UChar, Char, UShort, Short, UInt, Int, Float, Int64 and Double. 
    ;                   Note: Types must not contain spaces i.e. " UInt" or "UInt " will not work. 
    ;                   When an invalid type is passed the method returns NULL and sets ErrorLevel to -2
    ;       aOffsets* - A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which is to be written to.
    ; Return Values:
    ;       Non Zero -  Indicates success.
    ;       Zero     -  Indicates failure. Check errorLevel and A_LastError for more information
    ;       Null    -   An invalid type was passed. Errorlevel is set to -2
	
	write(address, value, type := "Uint", aOffsets*)
	{
		if !this.aTypeSize.hasKey(type)
			return "", ErrorLevel := -2 
		return DllCall("WriteProcessMemory", "Ptr", this.hProcess, "Ptr", aOffsets.maxIndex() ? this.getAddressFromOffsets(address, aOffsets*) : address, type "*", value, "Ptr", this.aTypeSize[type], "Ptr", this.pNumberOfBytesWritten) 
	}
	
    ; Method:   writeRaw(address, pBuffer, sizeBytes, aOffsets*)
    ;           Writes a buffer to the process.
    ; Parameters:
    ;   address -       The memory address to which the contents of the buffer will be written 
    ;                   or if using the offset parameter, the base address of the pointer.    
    ;   pBuffer -       A pointer to the buffer which is to be written.
    ;                   This does not necessarily have to be the beginning of the buffer itself e.g. pBuffer := &buffer + offset
    ;   sizeBytes -     The number of bytes which are to be written from the buffer.
    ;   aOffsets* -     A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which is to be written to.
    ; Return Values:
    ;       Non Zero -  Indicates success.
    ;       Zero     -  Indicates failure. Check errorLevel and A_LastError for more information
	
	writeRaw(address, pBuffer, sizeBytes, aOffsets*)
	{
		return DllCall("WriteProcessMemory", "Ptr", this.hProcess, "Ptr", aOffsets.maxIndex() ? this.getAddressFromOffsets(address, aOffsets*) : address, "Ptr", pBuffer, "Ptr", sizeBytes, "Ptr", this.pNumberOfBytesWritten) 
	}
	
    ; Method:   writeBytes(address, hexStringOrByteArray, aOffsets*)
    ;           Writes a sequence of byte values to the process.
    ; Parameters:
    ;   address -       The memory address to where the bytes will be written 
    ;                   or if using the offset parameter, the base address of the pointer.    
    ;   hexStringOrByteArray -  This can either be either a string (A) or an object/array (B) containing the values to be written.
    ;              
    ;               A) HexString -      A string of hex bytes.  The '0x' hex prefix is optional.
    ;                                   Bytes can optionally be separated using the space or tab characters.
    ;                                   Each byte must be two characters in length i.e. '04' or '0x04' (not '4' or '0x4') 
    ;               B) Object/Array -   An array containing hex or decimal byte values e.g. array := [10, 29, 0xA]
    ;
    ;   aOffsets* -     A variadic list of offsets. When using offsets the address parameter should equal the base address of the pointer.
    ;                   The address (base address) and offsets should point to the memory address which is to be written to.
    ; Return Values:
    ;       -1, -2, -3, -4  - Error with the hexstring. Refer to hexStringToPattern() for details.
    ;       Other Non Zero  - Indicates success.
    ;       Zero            - Indicates write failure. Check errorLevel and A_LastError for more information
    ;
    ;   Examples:
    ;                   writeBytes(0xAABBCC11, "DEADBEEF")          ; Writes the bytes DE AD BE EF starting at address  0xAABBCC11
    ;                   writeBytes(0xAABBCC11, [10, 20, 0xA, 2])
	
	writeBytes(address, hexStringOrByteArray, aOffsets*)
	{
		if !IsObject(hexStringOrByteArray)
		{
			if !IsObject(hexStringOrByteArray := this.hexStringToPattern(hexStringOrByteArray))
				return hexStringOrByteArray
		}
		sizeBytes := this.getNeedleFromAOBPattern("", buffer, hexStringOrByteArray*)
		return this.writeRaw(address, &buffer, sizeBytes, aOffsets*)
	}
	
    ; Method:           pointer(address, finalType := "UInt", offsets*)
    ;                   This is an internal method. Since the other various methods all offer this functionality, they should be used instead.
    ;                   This will read integer values of both pointers and non-pointers (i.e. a single memory address)
    ; Parameters:
    ;   address -       The base address of the pointer or the memory address for a non-pointer.
    ;   finalType -     The type of integer stored at the final address.
    ;                   Valid types are UChar, Char, UShort, Short, UInt, Int, Float, Int64 and Double. 
    ;                   Note: Types must not contain spaces i.e. " UInt" or "UInt " will not work. 
    ;                   When an invalid type is passed the method returns NULL and sets ErrorLevel to -2
    ;   aOffsets* -     A variadic list of offsets used to calculate the pointers final address.
    ; Return Values: (The same as the read() method)
    ;       integer -   Indicates success.
    ;       Null    -   Indicates failure. Check ErrorLevel and A_LastError for more information.
    ;       Note:       Since the returned integer value may be 0, to check for success/failure compare the result
    ;                   against null i.e. if (result = "") then an error has occurred.
    ;                   If the target application is 64bit the pointers are read as an 8 byte Int64 (this.PtrType)
	
	pointer(address, finalType := "UInt", offsets*)
	{ 
		For index, offset in offsets
			address := this.Read(address, this.ptrType) + offset 
		Return this.Read(address, finalType)
	}
	
    ; Method:               getAddressFromOffsets(address, aOffsets*)
    ;                       Returns the final address of a pointer.
    ;                       This is an internal method used by various methods however, this method may be useful if you are 
    ;                       looking to eliminate the overhead overhead associated with reading pointers which only change 
    ;                       on startup or map/level change. In other words you can cache the final address and
    ;                       read from this address directly.
    ; Parameters:
    ;   address             The base address of the pointer.
    ;   aOffsets*           A variadic list of offsets used to calculate the pointers final address.
    ;                       At least one offset must be present.
    ; Return Values:    
    ;   Positive integer    The final memory address pointed to by the pointer.
    ;   Negative integer    Failure
    ;   Null                Failure
    ; Note:                 If the target application is 64bit the pointers are read as an 8 byte Int64 (this.PtrType)
	
	getAddressFromOffsets(address, aOffsets*)
	{
		return  aOffsets.Remove() + this.pointer(address, this.ptrType, aOffsets*) ; remove the highest key so can use pointer() to find final memory address (minus the last offset)       
	}
	
    ; Interesting note:
    ; Although handles are 64-bit pointers, only the less significant 32 bits are employed in them for the purpose 
    ; of better compatibility (for example, to enable 32-bit and 64-bit processes interact with each other)
    ; Here are examples of such types: HANDLE, HWND, HMENU, HPALETTE, HBITMAP, etc. 
    ; http://www.viva64.com/en/k/0005/
	
	
	
    ; Method:   getProcessBaseAddress(WindowTitle, windowMatchMode := 3)
    ;           Returns the base address of a process. In most cases this will provide the same result as calling getModuleBaseAddress() (when passing 
    ;           a null value as the module parameter), however getProcessBaseAddress() will usually work regardless of the bitness
    ;           of both the AHK exe and the target process.
    ;           *This method relies on the target process having a window and will not work for console apps*
    ;           *'DetectHiddenWindows, On' is required for hidden windows*
    ;           ***If this returns an incorrect value, try using (the MORE RELIABLE) getModuleBaseAddress() instead.***
    ; Parameters:
    ;   windowTitle         This can be any AHK windowTitle identifier, such as 
    ;                       ahk_exe, ahk_class, ahk_pid, or simply the window title. e.g. "ahk_exe calc.exe" or "Calculator".
    ;                       It's safer not to use the window title, as some things can have the same window title e.g. an open folder called "Starcraft II"
    ;                       would have the same window title as the game itself.
    ;   windowMatchMode     Determines the matching mode used when finding the program's window (windowTitle).
    ;                       The default value is 3 i.e. an exact match. The current matchmode will be used if the parameter is null or 0.
    ;                       Refer to AHK's setTitleMathMode for more information.
    ; Return Values:
    ;   Positive integer    The base address of the process (success).
    ;   Null                The process's window couldn't be found.
    ;   0                   The GetWindowLong or GetWindowLongPtr call failed. Try getModuleBaseAddress() instead.
	
	
	getProcessBaseAddress(windowTitle, windowMatchMode := "3")   
	{
		if (windowMatchMode && A_TitleMatchMode != windowMatchMode)
		{
			mode := A_TitleMatchMode ; This is a string and will not contain the 0x prefix
			StringReplace, windowMatchMode, windowMatchMode, 0x ; remove hex prefix as SetTitleMatchMode will throw a run time error. This will occur if integer mode is set to hex and matchmode param is passed as an number not a string.
			SetTitleMatchMode, %windowMatchMode%    ;mode 3 is an exact match
		}
		WinGet, hWnd, ID, %WindowTitle%
		if mode
			SetTitleMatchMode, %mode%    ; In case executed in autoexec
		if !hWnd
			return ; return blank failed to find window
       ; GetWindowLong returns a Long (Int) and GetWindowLongPtr return a Long_Ptr
		return DllCall(A_PtrSize = 4     ; If DLL call fails, returned value will = 0
            ? "GetWindowLong"
            : "GetWindowLongPtr"
            , "Ptr", hWnd, "Int", -6, A_Is64bitOS ? "Int64" : "UInt")  
            ; For the returned value when the OS is 64 bit use Int64 to prevent negative overflow when AHK is 32 bit and target process is 64bit 
            ; however if the OS is 32 bit, must use UInt, otherwise the number will be huge (however it will still work as the lower 4 bytes are correct)      
            ; Note - it's the OS bitness which matters here, not the scripts/AHKs
	}   
	
    ; http://winprogger.com/getmodulefilenameex-enumprocessmodulesex-failures-in-wow64/
    ; http://stackoverflow.com/questions/3801517/how-to-enum-modules-in-a-64bit-process-from-a-32bit-wow-process
	
    ; Method:            getModuleBaseAddress(module := "", byRef aModuleInfo := "")
    ; Parameters:
    ;   moduleName -    The file name of the module/dll to find e.g. "calc.exe", "GDI32.dll", "Bass.dll" etc
    ;                   If no module (null) is specified, the address of the base module - main()/process will be returned 
    ;                   e.g. for calc.exe the following two method calls are equivalent getModuleBaseAddress() and getModuleBaseAddress("calc.exe")
    ;   aModuleInfo -   (Optional) A module Info object is returned in this variable. If method fails this variable is made blank.
    ;                   This object contains the keys: name, fileName, lpBaseOfDll, SizeOfImage, and EntryPoint 
    ; Return Values: 
    ;   Positive integer - The module's base/load address (success).
    ;   -1 - Module not found
    ;   -3 - EnumProcessModulesEx failed
    ;   -4 - The AHK script is 32 bit and you are trying to access the modules of a 64 bit target process. Or the target process has been closed.
    ; Notes:    A 64 bit AHK can enumerate the modules of a target 64 or 32 bit process.
    ;           A 32 bit AHK can only enumerate the modules of a 32 bit process
    ;           This method requires PROCESS_QUERY_INFORMATION + PROCESS_VM_READ access rights. These are included by default with this class.
	
	getModuleBaseAddress(moduleName := "", byRef aModuleInfo := "")
	{
		aModuleInfo := ""
		if (moduleName = "")
			moduleName := this.GetModuleFileNameEx(0, True) ; main executable module of the process - get just fileName no path
		if r := this.getModules(aModules, True) < 0
			return r ; -4, -3
		return aModules.HasKey(moduleName) ? (aModules[moduleName].lpBaseOfDll, aModuleInfo := aModules[moduleName]) : -1
        ; no longer returns -5 for failed to get module info
	}  
     
	
    ; Method:                   getModuleFromAddress(address, byRef aModuleInfo) 
    ;                           Finds the module in which the address resides. 
    ; Parameters:
    ;   address                 The address of interest.
    ;                       
    ;   aModuleInfo             (Optional) An unquoted variable name. If the module associated with the address is found,
    ;                           a moduleInfo object will be stored in this variable. This object has the 
    ;                           following keys: name, fileName, lpBaseOfDll, SizeOfImage, and EntryPoint. 
    ;                           If the address is not found to reside inside a module, the passed variable is
    ;                           made blank/null.
    ;   offsetFromModuleBase    (Optional) Stores the relative offset from the module base address 
    ;                           to the specified address. If the method fails then the passed variable is set to blank/empty.
    ; Return Values:
    ;   1                       Success - The address is contained within a module.
    ;   -1                      The specified address does not reside within a loaded module.
    ;   -3                      EnumProcessModulesEx failed.
    ;   -4                      The AHK script is 32 bit and you are trying to access the modules of a 64 bit target process.      
	
	getModuleFromAddress(address, byRef aModuleInfo, byRef offsetFromModuleBase := "") 
	{
		aModuleInfo := offsetFromModule := ""
		if result := this.getmodules(aModules) < 0
			return result ; error -3, -4
		for k, module in aModules 
		{
			if (address >= module.lpBaseOfDll && address < module.lpBaseOfDll + module.SizeOfImage)
				return 1, aModuleInfo := module, offsetFromModuleBase := address - module.lpBaseOfDll
		}    
		return -1    
	}
	
    ; SeDebugPrivileges is required to read/write memory in some programs.
    ; This only needs to be called once when the script starts,
    ; regardless of the number of programs being read (or if the target programs restart)
    ; Call this before attempting to call any other methods in this class 
    ; i.e. call _ClassMemory.setSeDebugPrivilege() at the very start of the script.
	
	setSeDebugPrivilege(enable := True)
	{
		h := DllCall("OpenProcess", "UInt", 0x0400, "Int", false, "UInt", DllCall("GetCurrentProcessId"), "Ptr")
        ; Open an adjustable access token with this process (TOKEN_ADJUST_PRIVILEGES = 32)
		DllCall("Advapi32.dll\OpenProcessToken", "Ptr", h, "UInt", 32, "PtrP", t)
		VarSetCapacity(ti, 16, 0)  ; structure of privileges
		NumPut(1, ti, 0, "UInt")  ; one entry in the privileges array...
        ; Retrieves the locally unique identifier of the debug privilege:
		DllCall("Advapi32.dll\LookupPrivilegeValue", "Ptr", 0, "Str", "SeDebugPrivilege", "Int64P", luid)
		NumPut(luid, ti, 4, "Int64")
		if enable
			NumPut(2, ti, 12, "UInt")  ; enable this privilege: SE_PRIVILEGE_ENABLED = 2
        ; Update the privileges of this process with the new access token:
		r := DllCall("Advapi32.dll\AdjustTokenPrivileges", "Ptr", t, "Int", false, "Ptr", &ti, "UInt", 0, "Ptr", 0, "Ptr", 0)
		DllCall("CloseHandle", "Ptr", t)  ; close this access token handle to save memory
		DllCall("CloseHandle", "Ptr", h)  ; close this process handle to save memory
		return r
	}
	
	
    ; Method:  isTargetProcess64Bit(PID, hProcess := "", currentHandleAccess := "")
    ;          Determines if a process is 64 bit.
    ; Parameters:
    ;   PID                     The Process ID of the target process. If required this is used to open a temporary process handle.  
    ;   hProcess                (Optional) A handle to the process, as returned by openProcess() i.e. [derivedObject].hProcess
    ;   currentHandleAccess     (Optional) The dwDesiredAccess value used when opening the process handle which has been 
    ;                           passed as the hProcess parameter. If specifying hProcess, you should also specify this value.                 
    ; Return Values:
    ;   True    The target application is 64 bit.
    ;   False   The target application is 32 bit.
    ;   Null    The method failed.
    ; Notes:
    ;   This is an internal method which is called when the new operator is used. It is used to set the pointer type for 32/64 bit applications so the pointer methods will work.
    ;   This operation requires a handle with PROCESS_QUERY_INFORMATION or PROCESS_QUERY_LIMITED_INFORMATION access rights.
    ;   If the currentHandleAccess parameter does not contain these rights (or not passed) or if the hProcess (process handle) is invalid (or not passed)
    ;   a temporary handle is opened to perform this operation. Otherwise if hProcess and currentHandleAccess appear valid  
    ;   the passed hProcess is used to perform the operation.
	
	isTargetProcess64Bit(PID, hProcess := "", currentHandleAccess := "")
	{
		if !A_Is64bitOS
			return False 
        ; If insufficient rights, open a temporary handle
		else if !hProcess || !(currentHandleAccess & (this.aRights.PROCESS_QUERY_INFORMATION | this.aRights.PROCESS_QUERY_LIMITED_INFORMATION))
			closeHandle := hProcess := this.openProcess(PID, this.aRights.PROCESS_QUERY_INFORMATION)
		if (hProcess && DllCall("IsWow64Process", "Ptr", hProcess, "Int*", Wow64Process))
			result := !Wow64Process
		return result, closeHandle ? this.CloseHandle(hProcess) : ""
	}
	/*
		_Out_  PBOOL Wow64Proces value set to:
		True if the process is running under WOW64 - 32bit app on 64bit OS.
		False if the process is running under 32-bit Windows!
		False if the process is a 64-bit application running under 64-bit Windows.
	*/  
	
    ; Method: suspend() / resume()
    ; Notes:
    ;   These are undocumented Windows functions which suspend and resume the process. Here be dragons.
    ;   The process handle must have PROCESS_SUSPEND_RESUME access rights. 
    ;   That is, you must specify this when using the new operator, as it is not included. 
    ;   Some people say it requires more rights and just use PROCESS_ALL_ACCESS, however PROCESS_SUSPEND_RESUME has worked for me.
    ;   Suspending a process manually can be quite helpful when reversing memory addresses and pointers, although it's not at all required. 
    ;   As an unorthodox example, memory addresses holding pointers are often stored in a slightly obfuscated manner i.e. they require bit operations to calculate their
    ;   true stored value (address). This obfuscation can prevent Cheat Engine from finding the true origin of a pointer or links to other memory regions. If there 
    ;   are no static addresses between the obfuscated address and the final destination address then CE wont find anything (there are ways around this in CE). One way around this is to
    ;   suspend the process, write the true/deobfuscated value to the address and then perform your scans. Afterwards write back the original values and resume the process.
	
	suspend()
	{
		return DllCall("ntdll\NtSuspendProcess", "Ptr", this.hProcess)
	}  
	
	resume()
	{
		return DllCall("ntdll\NtResumeProcess", "Ptr", this.hProcess)
	} 
	
    ; Method:               getModules(byRef aModules, useFileNameAsKey := False)
    ;                       Stores the process's loaded modules as an array of (object) modules in the aModules parameter.
    ; Parameters:
    ;   aModules            An unquoted variable name. The loaded modules of the process are stored in this variable as an array of objects.
    ;                       Each object in this array has the following keys: name, fileName, lpBaseOfDll, SizeOfImage, and EntryPoint. 
    ;   useFileNameAsKey    When true, the file name e.g. GDI32.dll is used as the lookup key for each module object.
    ; Return Values:
    ;   Positive integer    The size of the aModules array. (Success)
    ;   -3                  EnumProcessModulesEx failed.
    ;   -4                  The AHK script is 32 bit and you are trying to access the modules of a 64 bit target process.
	
	getModules(byRef aModules, useFileNameAsKey := False)
	{
		if (A_PtrSize = 4 && this.IsTarget64bit)
			return -4 ; AHK is 32bit and target process is 64 bit, this function wont work     
		aModules := []
		if !moduleCount := this.EnumProcessModulesEx(lphModule)
			return -3  
		loop % moduleCount
		{
			this.GetModuleInformation(hModule := numget(lphModule, (A_index - 1) * A_PtrSize), aModuleInfo)
			aModuleInfo.Name := this.GetModuleFileNameEx(hModule)
			filePath := aModuleInfo.name
			SplitPath, filePath, fileName
			aModuleInfo.fileName := fileName
			if useFileNameAsKey
				aModules[fileName] := aModuleInfo
			else aModules.insert(aModuleInfo)
		}
		return moduleCount        
	}
	
	
	
	getEndAddressOfLastModule(byRef aModuleInfo := "")
	{
		if !moduleCount := this.EnumProcessModulesEx(lphModule)
			return -3     
		hModule := numget(lphModule, (moduleCount - 1) * A_PtrSize)
		if this.GetModuleInformation(hModule, aModuleInfo)
			return aModuleInfo.lpBaseOfDll + aModuleInfo.SizeOfImage
		return -5
	}
	
    ; lpFilename [out]
    ; A pointer to a buffer that receives the fully qualified path to the module. 
    ; If the size of the file name is larger than the value of the nSize parameter, the function succeeds 
    ; but the file name is truncated and null-terminated.
    ; If the buffer is adequate the string is still null terminated. 
	
	GetModuleFileNameEx(hModule := 0, fileNameNoPath := False)
	{
        ; ANSI MAX_PATH = 260 (includes null) - unicode can be ~32K.... but no one would ever have one that size
        ; So just give it a massive size and don't bother checking. Most coders just give it MAX_PATH size anyway
		VarSetCapacity(lpFilename, 2048 * (A_IsUnicode ? 2 : 1)) 
		DllCall("psapi\GetModuleFileNameEx"
                    , "Ptr", this.hProcess
                    , "Ptr", hModule
                    , "Str", lpFilename
                    , "Uint", 2048 / (A_IsUnicode ? 2 : 1))
		if fileNameNoPath
			SplitPath, lpFilename, lpFilename ; strips the path so = GDI32.dll
		
		return lpFilename
	}
	
    ; dwFilterFlag
    ;   LIST_MODULES_DEFAULT    0x0  
    ;   LIST_MODULES_32BIT      0x01
    ;   LIST_MODULES_64BIT      0x02
    ;   LIST_MODULES_ALL        0x03
    ; If the function is called by a 32-bit application running under WOW64, the dwFilterFlag option 
    ; is ignored and the function provides the same results as the EnumProcessModules function.
	EnumProcessModulesEx(byRef lphModule, dwFilterFlag := 0x03)
	{
		lastError := A_LastError
		size := VarSetCapacity(lphModule, 4)
		loop 
		{
			DllCall("psapi\EnumProcessModulesEx"
                        , "Ptr", this.hProcess
                        , "Ptr", &lphModule
                        , "Uint", size
                        , "Uint*", reqSize
                        , "Uint", dwFilterFlag)
			if ErrorLevel
				return 0
			else if (size >= reqSize)
				break
			else size := VarSetCapacity(lphModule, reqSize)  
		}
        ; On first loop it fails with A_lastError = 0x299 as its meant to
        ; might as well reset it to its previous version
		DllCall("SetLastError", "UInt", lastError)
		return reqSize // A_PtrSize ; module count  ; sizeof(HMODULE) - enumerate the array of HMODULEs     
	}
	
	GetModuleInformation(hModule, byRef aModuleInfo)
	{
		VarSetCapacity(MODULEINFO, A_PtrSize * 3), aModuleInfo := []
		return DllCall("psapi\GetModuleInformation"
                    , "Ptr", this.hProcess
                    , "Ptr", hModule
                    , "Ptr", &MODULEINFO
                    , "UInt", A_PtrSize * 3)
                , aModuleInfo := {  lpBaseOfDll: numget(MODULEINFO, 0, "Ptr")
                                ,   SizeOfImage: numget(MODULEINFO, A_PtrSize, "UInt")
                                ,   EntryPoint: numget(MODULEINFO, A_PtrSize * 2, "Ptr") }
	}
	
    ; Method:           hexStringToPattern(hexString)
    ;                   Converts the hex string parameter into an array of bytes pattern (AOBPattern) that
    ;                   can be passed to the various pattern scan methods i.e.  modulePatternScan(), addressPatternScan(), rawPatternScan(), and processPatternScan()
    ;       
    ; Parameters:
    ;   hexString -     A string of hex bytes.  The '0x' hex prefix is optional.
    ;                   Bytes can optionally be separated using the space or tab characters.
    ;                   Each byte must be two characters in length i.e. '04' or '0x04' (not '4' or '0x4') 
    ;                   ** Unlike the other methods, wild card bytes MUST be denoted using '??' (two question marks)** 
    ;
    ; Return Values: 
    ;   Object          Success - The returned object contains the AOB pattern. 
    ;   -1              An empty string was passed.
    ;   -2              Non hex character present.  Acceptable characters are A-F, a-F, 0-9, ?, space, tab, and 0x (hex prefix).
    ;   -3              Non-even wild card character count. One of the wild card bytes is missing a '?' e.g. '?' instead of '??'.               
    ;   -4              Non-even character count. One of the hex bytes is probably missing a character e.g. '4' instead of '04'.
    ;
    ;   Examples:
    ;                   pattern := hexStringToPattern("DEADBEEF02")
    ;                   pattern := hexStringToPattern("0xDE0xAD0xBE0xEF0x02")
    ;                   pattern := hexStringToPattern("DE AD BE EF 02")
    ;                   pattern := hexStringToPattern("0xDE 0xAD 0xBE 0xEF 0x02")
    ;               
    ;                   This will mark the third byte as wild:
    ;                   pattern := hexStringToPattern("DE AD ?? EF 02")
    ;                   pattern := hexStringToPattern("0xDE 0xAD ?? 0xEF 0x02")
    ;               
    ;                   The returned pattern can then be passed to the various pattern scan methods, for example:
    ;                   pattern := hexStringToPattern("DE AD BE EF 02")
    ;                   memObject.processPatternScan(,, pattern*)   ; Note the '*'
	
	hexStringToPattern(hexString)
	{
		AOBPattern := []
		hexString := RegExReplace(hexString, "(\s|0x)")
		StringReplace, hexString, hexString, ?, ?, UseErrorLevel
		wildCardCount := ErrorLevel
		
		if !length := StrLen(hexString)
			return -1 ; no str
		else if RegExMatch(hexString, "[^0-9a-fA-F?]")
			return -2 ; non hex character and not a wild card
		else if Mod(wildCardCount, 2)
			return -3 ; non-even wild card character count
		else if Mod(length, 2)
			return -4 ; non-even character count
		loop, % length/2
		{
			value := "0x" SubStr(hexString, 1 + 2 * (A_index-1), 2)
			AOBPattern.Insert(value + 0 = "" ? "?" : value)
		}
		return AOBPattern
	}
	
    ; Method:           stringToPattern(string, encoding := "UTF-8", insertNullTerminator := False)
    ;                   Converts a text string parameter into an array of bytes pattern (AOBPattern) that
    ;                   can be passed to the various pattern scan methods i.e.  modulePatternScan(), addressPatternScan(), rawPatternScan(), and processPatternScan()
    ; 
    ; Parameters:
    ;   string                  The text string to convert.
    ;   encoding                This refers to how the string is stored in the program's memory.
    ;                           UTF-8 and UTF-16 are common. Refer to the AHK manual for other encoding types.  
    ;   insertNullTerminator    Includes the null terminating byte(s) (at the end of the string) in the AOB pattern.
    ;                           This should be set to 'false' unless you are certain that the target string is null terminated and you are searching for the entire string or the final part of the string.
    ;
    ; Return Values: 
    ;   Object          Success - The returned object contains the AOB pattern. 
    ;   -1              An empty string was passed.
    ;
    ;   Examples:
    ;                   pattern := stringToPattern("This text exists somewhere in the target program!")
    ;                   memObject.processPatternScan(,, pattern*)   ; Note the '*'
	
	stringToPattern(string, encoding := "UTF-8", insertNullTerminator := False)
	{   
		if !length := StrLen(string)
			return -1 ; no str  
		AOBPattern := []
		encodingSize := (encoding = "utf-16" || encoding = "cp1200") ? 2 : 1
		requiredSize := StrPut(string, encoding) * encodingSize - (insertNullTerminator ? 0 : encodingSize)
		VarSetCapacity(buffer, requiredSize)
		StrPut(string, &buffer, length + (insertNullTerminator ?  1 : 0), encoding) 
		loop, % requiredSize
			AOBPattern.Insert(NumGet(buffer, A_Index-1, "UChar"))
		return AOBPattern
	}    
	
	
    ; Method:           modulePatternScan(module := "", aAOBPattern*)
    ;                   Scans the specified module for the specified array of bytes    
    ; Parameters:
    ;   module -        The file name of the module/dll to search e.g. "calc.exe", "GDI32.dll", "Bass.dll" etc
    ;                   If no module (null) is specified, the executable file of the process will be used. 
    ;                   e.g. for calc.exe it would be the same as calling modulePatternScan(, aAOBPattern*) or modulePatternScan("calc.exe", aAOBPattern*)
    ;   aAOBPattern*    A variadic list of byte values i.e. the array of bytes to find.
    ;                   Wild card bytes should be indicated by passing a non-numeric value eg "?".
    ; Return Values:
    ;   Positive int    Success. The memory address of the found pattern.   
    ;   Null            Failed to find or retrieve the specified module. ErrorLevel is set to the returned error from getModuleBaseAddress()
    ;                   refer to that method for more information.
    ;   0               The pattern was not found inside the module
    ;   -9              VirtualQueryEx() failed
    ;   -10             The aAOBPattern* is invalid. No bytes were passed                   
	
	modulePatternScan(module := "", aAOBPattern*)
	{
		MEM_COMMIT := 0x1000, MEM_MAPPED := 0x40000, MEM_PRIVATE := 0x20000
        , PAGE_NOACCESS := 0x01, PAGE_GUARD := 0x100
		
		if (result := this.getModuleBaseAddress(module, aModuleInfo)) <= 0
			return "", ErrorLevel := result ; failed    
		if !patternSize := this.getNeedleFromAOBPattern(patternMask, AOBBuffer, aAOBPattern*)
			return -10 ; no pattern
        ; Try to read the entire module in one RPM()
        ; If fails with access (-1) iterate the modules memory pages and search the ones which are readable          
		if (result := this.PatternScan(aModuleInfo.lpBaseOfDll, aModuleInfo.SizeOfImage, patternMask, AOBBuffer)) >= 0
			return result  ; Found / not found
        ; else RPM() failed lets iterate the pages
		address := aModuleInfo.lpBaseOfDll
		endAddress := address + aModuleInfo.SizeOfImage
		loop 
		{
			if !this.VirtualQueryEx(address, aRegion)
				return -9
			if (aRegion.State = MEM_COMMIT 
            && !(aRegion.Protect & (PAGE_NOACCESS | PAGE_GUARD)) ; can't read these areas
            ;&& (aRegion.Type = MEM_MAPPED || aRegion.Type = MEM_PRIVATE) ;Might as well read Image sections as well
            && aRegion.RegionSize >= patternSize
            && (result := this.PatternScan(address, aRegion.RegionSize, patternMask, AOBBuffer)) > 0)
				return result
		} until (address += aRegion.RegionSize) >= endAddress
		return 0       
	}
	
    ; Method:               addressPatternScan(startAddress, sizeOfRegionBytes, aAOBPattern*)
    ;                       Scans a specified memory region for an array of bytes pattern.
    ;                       The entire memory area specified must be readable for this method to work,
    ;                       i.e. you must ensure the area is readable before calling this method.
    ; Parameters:
    ;   startAddress        The memory address from which to begin the search.
    ;   sizeOfRegionBytes   The numbers of bytes to scan in the memory region.
    ;   aAOBPattern*        A variadic list of byte values i.e. the array of bytes to find.
    ;                       Wild card bytes should be indicated by passing a non-numeric value eg "?".      
    ; Return Values:
    ;   Positive integer    Success. The memory address of the found pattern.
    ;   0                   Pattern not found
    ;   -1                  Failed to read the memory region.
    ;   -10                 An aAOBPattern pattern. No bytes were passed.
	
	addressPatternScan(startAddress, sizeOfRegionBytes, aAOBPattern*)
	{
		if !this.getNeedleFromAOBPattern(patternMask, AOBBuffer, aAOBPattern*)
			return -10
		return this.PatternScan(startAddress, sizeOfRegionBytes, patternMask, AOBBuffer)   
	}
	
    ; Method:       processPatternScan(startAddress := 0, endAddress := "", aAOBPattern*)
    ;               Scan the memory space of the current process for an array of bytes pattern. 
    ;               To use this in a loop (scanning for multiple occurrences of the same pattern),
    ;               simply call it again passing the last found address + 1 as the startAddress.
    ; Parameters:
    ;   startAddress -      The memory address from which to begin the search.
    ;   endAddress -        The memory address at which the search ends. 
    ;                       Defaults to 0x7FFFFFFF for 32 bit target processes.
    ;                       Defaults to 0xFFFFFFFF for 64 bit target processes when the AHK script is 32 bit.
    ;                       Defaults to 0x7FFFFFFFFFF for 64 bit target processes when the AHK script is 64 bit. 
    ;                       0x7FFFFFFF and 0x7FFFFFFFFFF are the maximum process usable virtual address spaces for 32 and 64 bit applications.
    ;                       Anything higher is used by the system (unless /LARGEADDRESSAWARE and 4GT have been modified).            
    ;                       Note: The entire pattern must be occur inside this range for a match to be found. The range is inclusive.
    ;   aAOBPattern* -      A variadic list of byte values i.e. the array of bytes to find.
    ;                       Wild card bytes should be indicated by passing a non-numeric value eg "?".
    ; Return Values:
    ;   Positive integer -  Success. The memory address of the found pattern.
    ;   0                   The pattern was not found.
    ;   -1                  VirtualQueryEx() failed.
    ;   -2                  Failed to read a memory region.
    ;   -10                 The aAOBPattern* is invalid. (No bytes were passed)
	
	processPatternScan(startAddress := 0, endAddress := "", aAOBPattern*)
	{
		address := startAddress
		if endAddress is not integer  
			endAddress := this.isTarget64bit ? (A_PtrSize = 8 ? 0x7FFFFFFFFFF : 0xFFFFFFFF) : 0x7FFFFFFF
		
		MEM_COMMIT := 0x1000, MEM_MAPPED := 0x40000, MEM_PRIVATE := 0x20000
		PAGE_NOACCESS := 0x01, PAGE_GUARD := 0x100
		if !patternSize := this.getNeedleFromAOBPattern(patternMask, AOBBuffer, aAOBPattern*)
			return -10  
		while address <= endAddress ; > 0x7FFFFFFF - definitely reached the end of the useful area (at least for a 32 target process)
		{
			if !this.VirtualQueryEx(address, aInfo)
				return -1
			if A_Index = 1
				aInfo.RegionSize -= address - aInfo.BaseAddress
			if (aInfo.State = MEM_COMMIT) 
            && !(aInfo.Protect & (PAGE_NOACCESS | PAGE_GUARD)) ; can't read these areas
            ;&& (aInfo.Type = MEM_MAPPED || aInfo.Type = MEM_PRIVATE) ;Might as well read Image sections as well
            && aInfo.RegionSize >= patternSize
            && (result := this.PatternScan(address, aInfo.RegionSize, patternMask, AOBBuffer))
			{
				if result < 0 
					return -2
				else if (result + patternSize - 1 <= endAddress)
					return result
				else return 0
			}
			address += aInfo.RegionSize
		}
		return 0
	}
	
    ; Method:           rawPatternScan(byRef buffer, sizeOfBufferBytes := "", aAOBPattern*)   
    ;                   Scans a binary buffer for an array of bytes pattern. 
    ;                   This is useful if you have already dumped a region of memory via readRaw()
    ; Parameters:
    ;   buffer              The binary buffer to be searched.
    ;   sizeOfBufferBytes   The size of the binary buffer. If null or 0 the size is automatically retrieved.
    ;   startOffset         The offset from the start of the buffer from which to begin the search. This must be >= 0.
    ;   aAOBPattern*        A variadic list of byte values i.e. the array of bytes to find.
    ;                       Wild card bytes should be indicated by passing a non-numeric value eg "?".
    ; Return Values:
    ;   >= 0                The offset of the pattern relative to the start of the haystack.
    ;   -1                  Not found.
    ;   -2                  Parameter incorrect.
	
	rawPatternScan(byRef buffer, sizeOfBufferBytes := "", startOffset := 0, aAOBPattern*)
	{
		if !this.getNeedleFromAOBPattern(patternMask, AOBBuffer, aAOBPattern*)
			return -10
		if (sizeOfBufferBytes + 0 = "" || sizeOfBufferBytes <= 0)
			sizeOfBufferBytes := VarSetCapacity(buffer)
		if (startOffset + 0 = "" || startOffset < 0)
			startOffset := 0
		return this.bufferScanForMaskedPattern(&buffer, sizeOfBufferBytes, patternMask, &AOBBuffer, startOffset)           
	}
	
    ; Method:           getNeedleFromAOBPattern(byRef patternMask, byRef needleBuffer, aAOBPattern*)
    ;                   Converts an array of bytes pattern (aAOBPattern*) into a binary needle and pattern mask string
    ;                   which are compatible with patternScan() and bufferScanForMaskedPattern().
    ;                   The modulePatternScan(), addressPatternScan(), rawPatternScan(), and processPatternScan() methods
    ;                   allow you to directly search for an array of bytes pattern in a single method call.
    ; Parameters:
    ;   patternMask -   (output) A string which indicates which bytes are wild/non-wild.
    ;   needleBuffer -  (output) The array of bytes passed via aAOBPattern* is converted to a binary needle and stored inside this variable.
    ;   aAOBPattern* -  (input) A variadic list of byte values i.e. the array of bytes from which to create the patternMask and needleBuffer.
    ;                   Wild card bytes should be indicated by passing a non-numeric value eg "?".
    ; Return Values:
    ;  The number of bytes in the binary needle and hence the number of characters in the patternMask string. 
	
	getNeedleFromAOBPattern(byRef patternMask, byRef needleBuffer, aAOBPattern*)
	{
		patternMask := "", VarSetCapacity(needleBuffer, aAOBPattern.MaxIndex())
		for i, v in aAOBPattern
			patternMask .= (v + 0 = "" ? "?" : "x"), NumPut(round(v), needleBuffer, A_Index - 1, "UChar")
		return round(aAOBPattern.MaxIndex())
	}
	
    ; The handle must have been opened with the PROCESS_QUERY_INFORMATION access right
	VirtualQueryEx(address, byRef aInfo)
	{
		
		if (aInfo.__Class != "_ClassMemory._MEMORY_BASIC_INFORMATION")
			aInfo := new this._MEMORY_BASIC_INFORMATION()
		return aInfo.SizeOfStructure = DLLCall("VirtualQueryEx" 
                                                , "Ptr", this.hProcess
                                                , "Ptr", address
                                                , "Ptr", aInfo.pStructure
                                                , "Ptr", aInfo.SizeOfStructure
                                                , "Ptr") 
	}
	
	/*
		// The c++ function used to generate the machine code
		int scan(unsigned char* haystack, unsigned int haystackSize, unsigned char* needle, unsigned int needleSize, char* patternMask, unsigned int startOffset)
		{
			for (unsigned int i = startOffset; i <= haystackSize - needleSize; i++)
			{
				for (unsigned int j = 0; needle[j] == haystack[i + j] || patternMask[j] == '?'; j++)
				{
					if (j + 1 == needleSize)
						return i;
				}
			}
			return -1;
		}
	*/
	
    ; Method:               PatternScan(startAddress, sizeOfRegionBytes, patternMask, byRef needleBuffer)
    ;                       Scans a specified memory region for a binary needle pattern using a machine code function
    ;                       If found it returns the memory address of the needle in the processes memory.
    ; Parameters:
    ;   startAddress -      The memory address from which to begin the search.
    ;   sizeOfRegionBytes - The numbers of bytes to scan in the memory region.
    ;   patternMask -       This string indicates which bytes must match and which bytes are wild. Each wildcard byte must be denoted by a single '?'. 
    ;                       Non wildcards can use any other single character e.g 'x'. There should be no spaces.
    ;                       With the patternMask 'xx??x', the first, second, and fifth bytes must match. The third and fourth bytes are wild.
    ;    needleBuffer -     The variable which contains the binary needle. This needle should consist of UChar bytes.
    ; Return Values:
    ;   Positive integer    The address of the pattern.
    ;   0                   Pattern not found.
    ;   -1                  Failed to read the region.
	
	patternScan(startAddress, sizeOfRegionBytes, byRef patternMask, byRef needleBuffer)
	{
		if !this.readRaw(startAddress, buffer, sizeOfRegionBytes)
			return -1      
		if (offset := this.bufferScanForMaskedPattern(&buffer, sizeOfRegionBytes, patternMask, &needleBuffer)) >= 0
			return startAddress + offset 
		else return 0
	}
    ; Method:               bufferScanForMaskedPattern(byRef hayStack, sizeOfHayStackBytes, byRef patternMask, byRef needle)
    ;                       Scans a binary haystack for binary needle against a pattern mask string using a machine code function.
    ; Parameters:
    ;   hayStackAddress -   The address of the binary haystack which is to be searched.
    ;   sizeOfHayStackBytes The total size of the haystack in bytes.
    ;   patternMask -       A string which indicates which bytes must match and which bytes are wild. Each wildcard byte must be denoted by a single '?'. 
    ;                       Non wildcards can use any other single character e.g 'x'. There should be no spaces.
    ;                       With the patternMask 'xx??x', the first, second, and fifth bytes must match. The third and fourth bytes are wild.
    ;   needleAddress -     The address of the binary needle to find. This needle should consist of UChar bytes.
    ;   startOffset -       The offset from the start of the haystack from which to begin the search. This must be >= 0.
    ; Return Values:    
    ;   >= 0                Found. The pattern begins at this offset - relative to the start of the haystack.
    ;   -1                  Not found.
    ;   -2                  Invalid sizeOfHayStackBytes parameter - Must be > 0.
	
    ; Notes:
    ;       This is a basic function with few safeguards. Incorrect parameters may crash the script.
	
	bufferScanForMaskedPattern(hayStackAddress, sizeOfHayStackBytes, byRef patternMask, needleAddress, startOffset := 0)
	{
		static p
		if !p
		{
			if A_PtrSize = 4    
				p := this.MCode("1,x86:8B44240853558B6C24182BC5568B74242489442414573BF0773E8B7C241CBB010000008B4424242BF82BD8EB038D49008B54241403D68A0C073A0A740580383F750B8D0C033BCD74174240EBE98B442424463B74241876D85F5E5D83C8FF5BC35F8BC65E5D5BC3")
			else 
				p := this.MCode("1,x64:48895C2408488974241048897C2418448B5424308BF2498BD8412BF1488BF9443BD6774A4C8B5C24280F1F800000000033C90F1F400066660F1F840000000000448BC18D4101418D4AFF03C80FB60C3941380C18740743803C183F7509413BC1741F8BC8EBDA41FFC2443BD676C283C8FF488B5C2408488B742410488B7C2418C3488B5C2408488B742410488B7C2418418BC2C3")
		}
		if (needleSize := StrLen(patternMask)) + startOffset > sizeOfHayStackBytes
			return -1 ; needle can't exist inside this region. And basic check to prevent wrap around error of the UInts in the machine function       
		if (sizeOfHayStackBytes > 0)
			return DllCall(p, "Ptr", hayStackAddress, "UInt", sizeOfHayStackBytes, "Ptr", needleAddress, "UInt", needleSize, "AStr", patternMask, "UInt", startOffset, "cdecl int")
		return -2
	}
	
    ; Notes: 
    ; Other alternatives for non-wildcard buffer comparison.
    ; Use memchr to find the first byte, then memcmp to compare the remainder of the buffer against the needle and loop if it doesn't match
    ; The function FindMagic() by Lexikos uses this method.
    ; Use scanInBuf() machine code function - but this only supports 32 bit ahk. I could check if needle contains wild card and AHK is 32bit,
    ; then call this function. But need to do a speed comparison to see the benefits, but this should be faster. Although the benefits for 
    ; the size of the memory regions be dumped would most likely be inconsequential as it's already extremely fast.
	
	MCode(mcode)
	{
		static e := {1:4, 2:1}, c := (A_PtrSize=8) ? "x64" : "x86"
		if !regexmatch(mcode, "^([0-9]+),(" c ":|.*?," c ":)([^,]+)", m)
			return
		if !DllCall("crypt32\CryptStringToBinary", "str", m3, "uint", 0, "uint", e[m1], "ptr", 0, "uint*", s, "ptr", 0, "ptr", 0)
			return
		p := DllCall("GlobalAlloc", "uint", 0, "ptr", s, "ptr")
        ; if (c="x64") ; Virtual protect must always be enabled for both 32 and 64 bit. If DEP is set to all applications (not just systems), then this is required
		DllCall("VirtualProtect", "ptr", p, "ptr", s, "uint", 0x40, "uint*", op)
		if DllCall("crypt32\CryptStringToBinary", "str", m3, "uint", 0, "uint", e[m1], "ptr", p, "uint*", s, "ptr", 0, "ptr", 0)
			return p
		DllCall("GlobalFree", "ptr", p)
		return
	}
	
    ; This link indicates that the _MEMORY_BASIC_INFORMATION32/64 should be based on the target process
    ; http://stackoverflow.com/questions/20068219/readprocessmemory-on-a-64-bit-proces-always-returns-error-299 
    ; The msdn documentation is unclear, and suggests that a debugger can pass either structure - perhaps there is some other step involved.
    ; My tests seem to indicate that you must pass _MEMORY_BASIC_INFORMATION i.e. structure is relative to the AHK script bitness.
    ; Another post on the net also agrees with my results. 
	
    ; Notes: 
    ; A 64 bit AHK script can call this on a target 64 bit process. Issues may arise at extremely high memory addresses as AHK does not support UInt64 (but these addresses should never be used anyway).
    ; A 64 bit AHK can call this on a 32 bit target and it should work. 
    ; A 32 bit AHk script can call this on a 64 bit target and it should work providing the addresses fall inside the 32 bit range.
	
	class _MEMORY_BASIC_INFORMATION
	{
		__new()
		{   
			if !this.pStructure := DllCall("GlobalAlloc", "UInt", 0, "Ptr", this.SizeOfStructure := A_PtrSize = 8 ? 48 : 28, "Ptr")
				return ""
			return this
		}
		__Delete()
		{
			DllCall("GlobalFree", "Ptr", this.pStructure)
		}
        ; For 64bit the int64 should really be unsigned. But AHK doesn't support these
        ; so this won't work correctly for higher memory address areas
		__get(key)
		{
			static aLookUp := A_PtrSize = 8 
                                ?   {   "BaseAddress": {"Offset": 0, "Type": "Int64"}
                                    ,    "AllocationBase": {"Offset": 8, "Type": "Int64"}
                                    ,    "AllocationProtect": {"Offset": 16, "Type": "UInt"}
                                    ,    "RegionSize": {"Offset": 24, "Type": "Int64"}
                                    ,    "State": {"Offset": 32, "Type": "UInt"}
                                    ,    "Protect": {"Offset": 36, "Type": "UInt"}
                                    ,    "Type": {"Offset": 40, "Type": "UInt"} }
                                :   {  "BaseAddress": {"Offset": 0, "Type": "UInt"}
                                    ,   "AllocationBase": {"Offset": 4, "Type": "UInt"}
                                    ,   "AllocationProtect": {"Offset": 8, "Type": "UInt"}
                                    ,   "RegionSize": {"Offset": 12, "Type": "UInt"}
                                    ,   "State": {"Offset": 16, "Type": "UInt"}
                                    ,   "Protect": {"Offset": 20, "Type": "UInt"}
                                    ,   "Type": {"Offset": 24, "Type": "UInt"} }
			
			if aLookUp.HasKey(key)
				return numget(this.pStructure+0, aLookUp[key].Offset, aLookUp[key].Type)        
		}
		__set(key, value)
		{
			static aLookUp := A_PtrSize = 8 
                                ?   {   "BaseAddress": {"Offset": 0, "Type": "Int64"}
                                    ,    "AllocationBase": {"Offset": 8, "Type": "Int64"}
                                    ,    "AllocationProtect": {"Offset": 16, "Type": "UInt"}
                                    ,    "RegionSize": {"Offset": 24, "Type": "Int64"}
                                    ,    "State": {"Offset": 32, "Type": "UInt"}
                                    ,    "Protect": {"Offset": 36, "Type": "UInt"}
                                    ,    "Type": {"Offset": 40, "Type": "UInt"} }
                                :   {  "BaseAddress": {"Offset": 0, "Type": "UInt"}
                                    ,   "AllocationBase": {"Offset": 4, "Type": "UInt"}
                                    ,   "AllocationProtect": {"Offset": 8, "Type": "UInt"}
                                    ,   "RegionSize": {"Offset": 12, "Type": "UInt"}
                                    ,   "State": {"Offset": 16, "Type": "UInt"}
                                    ,   "Protect": {"Offset": 20, "Type": "UInt"}
                                    ,   "Type": {"Offset": 24, "Type": "UInt"} }
			
			if aLookUp.HasKey(key)
			{
				NumPut(value, this.pStructure+0, aLookUp[key].Offset, aLookUp[key].Type)            
				return value
			}
		}
		Ptr()
		{
			return this.pStructure
		}
		sizeOf()
		{
			return this.SizeOfStructure
		}
	}
	
}