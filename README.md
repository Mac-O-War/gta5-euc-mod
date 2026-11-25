# EUC Mod for GTA V

Ride electric unicycles in GTA V single-player (Legacy). Script-driven (ScriptHookVDotNet v3)
with an add-on prop model packed as a DLC.

## Layout
- `src/EucMod/` – C# script (`EucScript.cs`), settings (`EucMod.ini`)
- `assets/source/` – downloaded source models
- `assets/blender/` – cleaned Blender files (`euc_offroad.blend`)
- `tools/blender/build_euc.py` – Blender → `.ydr` + `.ytyp` (Sollumz, native Gen8)
- `tools/blender/dds_bc1.py` – DXT1 texture encoder (Sollumz only embeds DDS)
- `tools/build_dlc.py` – packs assets into `dlc.rpf` (PyMateria)
- `build.ps1` – runs the whole pipeline

## Build
```
.\build.ps1             # model + DLC + script, deploys to the game
.\build.ps1 -SkipModel  # script only
```

## One-time setup
1. **ScriptHookV** + **ScriptHookVDotNet nightly** (v3.7.0-nightly or newer; stable 3.6.0 crashes on
   game builds ≥ 1.0.3258). SHVDN `ReloadKeyBinding=Insert`.
2. **Project Blender profile** (Blender 4.3, Sollumz 2.9.1 + szio + PyMateria) lives in
   `tools/blender_user` (git-ignored). Recreate with:
   ```
   $env:BLENDER_USER_RESOURCES="C:\code\gta5_euc\tools\blender_user"
   blender --factory-startup -b --online-mode --command extension install-file -r user_default -e Sollumz.zip
   blender -b --online-mode -P tools\blender\setup_sollumz.py -- --with-pymateria
   ```
3. **OpenIV mods folder** so the game loads the DLC pack:
   - Install OpenIV, point it at the GTA V Legacy folder.
   - Tools → ASI Manager → install **OpenIV.ASI**.
   - Turn on **Edit mode**, open `update\update.rpf\common\data\dlclist.xml`, accept copying
     `update.rpf` to the `mods` folder, and add before `</Paths>`:
     `<Item>dlcpacks:/eucmod/</Item>`
   - Run `.\build.ps1`; it copies `dlc.rpf` to `mods\update\x64\dlcpacks\eucmod\`.

4. **Rider animation tools** (used by `build.ps1`):
   - `tools/downloads/CodeWalker`: shallow git clone of github.com/dexyfex/CodeWalker. `tools/ycdconv`
     builds against its Core library to convert Sollumz `.ycd.xml` to binary `.ycd`.
   - `assets/game_extract/player_zero_rig.blend`: player_zero.yft + body .ydd files extracted from
     `x64v.rpf/models/cdimages/streamedpeds_players.rpf` and imported with Sollumz onto one armature.
     Game files stay git-ignored and are only used for authoring.
   - Poses live in `tools/blender/anim/build_euc_anims.py` (reference notes: `assets/reference/NOTES.md`).

## Controls
- **F7** – spawn / remove the EUC
- **F** (Enter) – ride / dismount
- **W/S** or left stick – accelerate / brake / reverse
- **A/D** – turn (works standing still)
- **Insert** – reload scripts after rebuilding or editing the ini

Story Mode only.
