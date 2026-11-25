# How to install the EUC mod (electric unicycle for GTA V)

Ride an electric unicycle around Los Santos. This guide is for players: no coding needed.
It takes about 15 minutes the first time, mostly installing the three standard modding tools
that most GTA V mods need anyway.

> **Story Mode only.** Never go into GTA Online with mods installed. You can get banned.
> The mod tools used here switch themselves off in Online, but don't take the risk.

---

## What you need

- **GTA V Legacy for PC** (Steam, Epic or Rockstar Launcher).
  The mod was tested on game version 1.0.3889. It has not been tested on **GTA V Enhanced**.
- The mod download **`EucMod-1.0.zip`**. Unzip it somewhere easy, like your Desktop. Inside you'll find:
  - `EucMod.oiv`: the unicycle model and animations (installed with OpenIV)
  - `scripts\`: a folder with `EucMod.dll` and `EucMod.ini` (the mod itself)
  - `INSTALL.md`: this guide

You'll also need three free tools. If you've used other GTA V mods before, you probably have them already:

| Tool | What it does |
|---|---|
| **Script Hook V** | Lets the game run mods |
| **Script Hook V .NET** | Lets the game run mods written in C#, like this one |
| **OpenIV** | Installs the unicycle's 3D model and animations |

---

## Step 1: Find your GTA V folder

You'll copy files into this folder, so find it first. Common places:

- Rockstar Launcher: `C:\Program Files\Rockstar Games\Grand Theft Auto V Legacy`
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V`
- Epic: `C:\Program Files\Epic Games\GTAV`

You've found the right folder when you can see **`GTA5.exe`** in it.

> **Tip:** When you copy files into this folder, Windows may say *"You'll need to provide
> administrator permission"*. Click **Continue**. That's normal.

---

## Step 2: Install Script Hook V

*(Skip if you already have it.)*

1. Go to **dev-c.com/gtav/scripthookv** and download the latest version.
   Check that it supports your game version. The site lists the supported versions.
2. Open the zip, go into its **`bin`** folder, and copy these two files into your GTA V folder:
   - `ScriptHookV.dll`
   - `dinput8.dll`

---

## Step 3: Install Script Hook V .NET (nightly version)

> ⚠️ **Important:** The "stable" release from 2022 **crashes** on current versions of the game.
> You need the **nightly** version, even if you already have Script Hook V .NET installed.

1. Go to **github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases**.
2. Download the newest `ScriptHookVDotNet-v3.7.0-nightly.___.zip`.
3. Open the zip and copy these files into your GTA V folder:
   - `ScriptHookVDotNet-Loader.asi`
   - `ScriptHookVDotNet.dll`
   - `ScriptHookVDotNet2.dll`
   - `ScriptHookVDotNet3.dll`
   - `ScriptHookVDotNet.ini`
4. If you have an old file called **`ScriptHookVDotNet.asi`** (without "-Loader") in your GTA V
   folder from an older install, **delete it**.

---

## Step 4: Install OpenIV

*(Skip if you already have OpenIV with OpenIV.ASI installed.)*

1. Download OpenIV from **openiv.com** and install it.
2. Start OpenIV, choose **GTA V → Windows**, and point it at your GTA V folder.
3. In OpenIV's menu, open **Tools → ASI Manager** and click **Install** next to **OpenIV.ASI**.
   If it also offers to install the **ASI Loader**, install that too.

---

## Step 5: Install the EUC mod

**A. The unicycle model** (the `.oiv` file)

1. **Double-click `EucMod.oiv`.** OpenIV's Package Installer opens.
   If double-clicking doesn't open it, start OpenIV and use **Tools → Package Installer**,
   then pick `EucMod.oiv`.
2. Click **Install**.
3. When it asks where to install, choose the **"mods" folder**. Never choose to install into the game folder.
4. Wait for **"Installation complete"**. The first time, OpenIV copies a large game file into
   the mods folder, so it can take a minute.

**B. The script** (the `scripts` folder)

5. Copy the **`scripts`** folder from the download into your **GTA V folder**.
   If Windows says a `scripts` folder already exists, choose to **merge** it.

When you're done, you should have `EucMod.dll` and `EucMod.ini` inside `<your GTA V folder>\scripts\`.

---

## Step 6: Play

1. Start GTA V **Story Mode**.
   - The game uses **BattlEye** anti-cheat for Online, and it blocks mods. If mods don't load,
     look in your launcher's GTA V settings for an option to launch **without BattlEye**,
     and play Story Mode that way.
2. Wait until you're in control of your character (not during a cutscene).
3. Press **F7** and a unicycle appears in front of you.
4. Walk up to it and press **F** to hop on.

### Controls

| Key | What it does |
|---|---|
| **F7** | Spawn / remove the unicycle |
| **F** | Get on / get off |
| **W** / **S** | Accelerate / brake (hold **S** when stopped to reverse) |
| **A** / **D** | Turn (you can spin on the spot) |
| **L** | Lights: Auto (on at night) → On → Off |
| **R** | Radio on / off |
| **[** / **]** | Previous / next radio station |

A controller's left stick works for riding and steering too.

### Things to know

- **Tilt-back:** near top speed the wheel beeps and pushes you back. If you keep pushing
  forward, you'll **overlean** and get thrown off, just like a real EUC.
- **Crashes:** hitting something fast or landing a big jump too hard will throw you off.
- **Battery:** shown bottom-right next to your speed. It drains as you ride, and recharges slowly
  while the wheel is parked. When it's low, top speed drops.
- **Water:** EUCs don't swim.

---

## Changing settings (optional)

Open **`scripts\EucMod.ini`** with Notepad to change things like top speed, how sharply it turns,
keys, or switching the speedometer to km/h (`SpeedUnits = kmh` under `[Visual]`).
Save the file, then restart the game to apply your changes.

---

## Troubleshooting

**Nothing happens when I press F7**
- Make sure you're in Story Mode, in control of your character, and not in a cutscene.
- Check that `EucMod.dll` is in `<GTA V folder>\scripts\` (Step 5B).
- Check that all the Script Hook V .NET files from Step 3 are in the GTA V folder, and that you
  used the **nightly** version.
- Look for **`ScriptHookVDotNet.log`** in your GTA V folder. Errors are listed there.

**The game crashes when I press F7 or when loading**
- Almost always caused by the old **stable** Script Hook V .NET (3.6.0). Replace it with the
  **nightly** version (Step 3) and delete the old `ScriptHookVDotNet.asi`.

**I get a tire (or a traffic cone) instead of a unicycle**
- The unicycle model isn't loading. Check that:
  - you installed `EucMod.oiv` into the **mods** folder (Step 5A). Run it again if unsure.
  - **OpenIV.ASI** is installed (Step 4, point 3).
- Restart the game after fixing it. The model only loads at startup.

**Mods stopped working after a GTA V update**
- Game updates break Script Hook V until its author releases an update. Check dev-c.com for a new
  version, and update Script Hook V .NET nightly too.

**No radio sound**
- Missions can mute the radio. Try it in free roam.

---

## Updating to a new version

Install the new `EucMod.oiv` the same way (Step 5A), then copy the new `scripts` folder over the
old one (Step 5B). If you changed settings in `EucMod.ini`, back it up first and copy your
changes back afterwards.

---

## Uninstalling

1. Delete `EucMod.dll` and `EucMod.ini` from `<GTA V folder>\scripts\`.
2. Delete the folder `<GTA V folder>\mods\update\x64\dlcpacks\eucmod`.

That's enough to stop the mod from loading. A leftover entry for it in the game's DLC list is
harmless.

To remove *all* mods, also delete the `mods` folder, `ScriptHookV.dll`, `dinput8.dll`,
`OpenIV.asi` and the `ScriptHookVDotNet*` files.
