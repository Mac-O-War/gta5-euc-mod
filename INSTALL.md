# How to install the EUC mod (electric unicycle for GTA V)

Ride an electric unicycle around Los Santos. This guide is for players: no coding needed.
It takes about 20–30 minutes the first time.

> **Story Mode only.** Never go into GTA Online with mods installed. You can get banned.
> The mod tools used here switch themselves off in Online, but don't take the risk.

---

## What you need

- **GTA V Legacy for PC** (Steam, Epic or Rockstar Launcher).
  The mod was tested on game version 1.0.3889. It has not been tested on **GTA V Enhanced**.
- The **EUC mod files**:
  - `EucMod.dll`
  - `EucMod.ini`
  - `dlc.rpf`
- About 2 GB of free disk space. OpenIV makes a backup copy of one game file.

You'll also download three free tools in the steps below:

| Tool | What it does |
|---|---|
| **Script Hook V** | Lets the game run mods |
| **Script Hook V .NET** | Lets the game run mods written in C#, like this one |
| **OpenIV** | Lets the game load new 3D models, like the unicycle |

---

## Step 1: Find your GTA V folder

You'll copy files into this folder several times, so find it first. Common places:

- Rockstar Launcher: `C:\Program Files\Rockstar Games\Grand Theft Auto V Legacy`
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V`
- Epic: `C:\Program Files\Epic Games\GTAV`

You've found the right folder when you can see **`GTA5.exe`** in it.

> **Tip:** When you copy files into this folder, Windows may say *"You'll need to provide
> administrator permission"*. Click **Continue**. That's normal.

---

## Step 2: Install Script Hook V

1. Go to **dev-c.com/gtav/scripthookv** and download the latest version.
   Check that it supports your game version. The site lists the supported versions.
2. Open the zip, go into its **`bin`** folder, and copy these two files into your GTA V folder:
   - `ScriptHookV.dll`
   - `dinput8.dll`

---

## Step 3: Install Script Hook V .NET (nightly version)

> ⚠️ **Important:** The "stable" release from 2022 **crashes** on current versions of the game.
> You need the **nightly** version.

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
5. Create a new folder called **`scripts`** inside your GTA V folder, unless one already exists.

---

## Step 4: Install OpenIV and set up the "mods" folder

The unicycle's 3D model and animations are a small add-on pack. OpenIV lets the game load it
without changing your original game files.

1. Download OpenIV from **openiv.com** and install it.
2. Start OpenIV, choose **GTA V → Windows**, and point it at your GTA V folder.
3. In OpenIV's menu, open **Tools → ASI Manager** and click **Install** next to **OpenIV.ASI**.
   If it also offers to install the **ASI Loader**, install that too.

### Tell the game about the unicycle pack

4. Click the **Edit mode** button at the top of OpenIV. When it warns you, click **Yes**.
5. In the file list, open: **update → update.rpf → common → data**.
6. Right-click **`dlclist.xml`** and choose **Edit**.
7. OpenIV will ask to copy `update.rpf` to the **mods** folder. Click **Copy to "mods" folder**.
   It's a big file, so wait for it to finish.
8. A text editor opens. Scroll to the **very bottom**. Just **above** the line `</Paths>`, add this line:

   ```
   <Item>dlcpacks:/eucmod/</Item>
   ```

   The end of the file should now look like this:

   ```
   		<Item>dlcpacks:/patch2026_01/</Item>
   		<Item>dlcpacks:/eucmod/</Item>
   	</Paths>
   </SMandatoryPacksData>
   ```

   > ⚠️ The line must be **inside** `<Paths> … </Paths>`. If you put it near the top of the
   > file, the game will ignore it and you'll get a tire instead of a unicycle.

9. Click **Save** and close the editor.

---

## Step 5: Copy in the EUC mod files

1. Copy **`EucMod.dll`** and **`EucMod.ini`** into the **`scripts`** folder inside your GTA V folder.
2. In your GTA V folder, create these folders: **`mods\update\x64\dlcpacks\eucmod`**.
   The `mods` and `update` folders may already exist from Step 4.
3. Copy **`dlc.rpf`** into that new **`eucmod`** folder.

When you're done, your GTA V folder should contain:

```
GTA5.exe
ScriptHookV.dll
dinput8.dll
OpenIV.asi
ScriptHookVDotNet-Loader.asi
ScriptHookVDotNet.dll  (+ ScriptHookVDotNet2.dll, ScriptHookVDotNet3.dll, ScriptHookVDotNet.ini)
scripts\
    EucMod.dll
    EucMod.ini
mods\
    update\
        update.rpf
        x64\dlcpacks\eucmod\
            dlc.rpf
```

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
- Check that `EucMod.dll` is in the `scripts` folder.
- Check that all the Script Hook V .NET files from Step 3 are in the GTA V folder, and that you
  used the **nightly** version.
- Look for **`ScriptHookVDotNet.log`** in your GTA V folder. Errors are listed there.

**The game crashes when I press F7 or when loading**
- Almost always caused by the old **stable** Script Hook V .NET (3.6.0). Replace it with the
  **nightly** version (Step 3) and delete the old `ScriptHookVDotNet.asi`.

**I get a tire (or a traffic cone) instead of a unicycle**
- The unicycle pack isn't loading. Check that:
  - `dlc.rpf` is at `mods\update\x64\dlcpacks\eucmod\dlc.rpf` (spelling matters)
  - the `dlcpacks:/eucmod/` line is **inside** `<Paths>` in `dlclist.xml` (Step 4)
  - **OpenIV.ASI** is installed (Step 4, point 3)
- Restart the game after fixing it. The pack only loads at startup.

**Mods stopped working after a GTA V update**
- Game updates break Script Hook V until its author releases an update. Check dev-c.com for a new
  version, and update Script Hook V .NET nightly too.

**No radio sound**
- Missions can mute the radio. Try it in free roam.

---

## Uninstalling

Delete these and the game is back to normal:

- `scripts\EucMod.dll` and `scripts\EucMod.ini`
- the `mods\update\x64\dlcpacks\eucmod` folder
- the `<Item>dlcpacks:/eucmod/</Item>` line in `mods\update\update.rpf → common\data\dlclist.xml`
  (edit it with OpenIV the same way as Step 4)

To remove *all* mods, also delete the `mods` folder, `ScriptHookV.dll`, `dinput8.dll`,
`OpenIV.asi` and the `ScriptHookVDotNet*` files.
