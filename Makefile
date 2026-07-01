KSPDIR  ?= $(HOME)/Library/Application Support/Steam/steamapps/common/Kerbal Space Program
MANAGED  = $(KSPDIR)/KSP.app/Contents/Resources/Data/Managed
GAMEDATA = $(KSPDIR)/GameData

VERSION  = 0.1.0
ZIPNAME  = kOS-AFBW-v$(VERSION).zip
OUTDIR   = bin/Debug
DLL      = $(OUTDIR)/kOS-AFBW.dll

REFS_FLAGS = \
	-r:"$(MANAGED)/Assembly-CSharp.dll" \
	-r:"$(MANAGED)/UnityEngine.dll" \
	-r:"$(MANAGED)/UnityEngine.CoreModule.dll" \
	-r:"$(GAMEDATA)/kOS/Plugins/kOS.dll" \
	-r:"$(GAMEDATA)/kOS/Plugins/kOS.Safe.dll" \
	-r:"$(GAMEDATA)/001_ToolbarControl/Plugins/ToolbarControl.dll" \
	-r:"$(GAMEDATA)/ksp-advanced-flybywire/Plugins/AdvancedFlyByWire.dll"

.PHONY: build package clean

build: $(DLL)

$(DLL): AFBWAddon.cs
	mkdir -p "$(OUTDIR)"
	mcs -t:library -out:"$(DLL)" $(REFS_FLAGS) AFBWAddon.cs

package: build
	rm -rf _staging
	mkdir -p _staging/GameData/kOS-AFBW/Plugins
	cp $(DLL) _staging/GameData/kOS-AFBW/Plugins/
	cp kOS-AFBW.version _staging/GameData/kOS-AFBW/
	cd _staging && zip -r ../$(ZIPNAME) GameData
	rm -rf _staging

clean:
	rm -rf bin/ _staging/
	rm -f $(ZIPNAME)
