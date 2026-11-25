"""Build the player release: EucMod.oiv (OpenIV package) + scripts folder + install guide, zipped.

The .oiv only carries what OpenIV installs reliably: the add-on pack (dlc.rpf) and the
dlclist.xml entry that makes the game load it. OpenIV puts both in the "mods" folder.
The two script files are shipped as a plain scripts\\ folder the player copies into the game
folder (Script Hook V .NET loads scripts from <GTA V>\\scripts, not from mods\\).

Format: OpenIV package 2.1 (zip renamed to .oiv with assembly.xml + content\\ at the root),
per https://github.com/5mods/tutorials/wiki/Making-an-.OIV-Installer-from-scratch

usage: python tools/build_oiv.py <dlc.rpf> <EucMod.dll> <EucMod.ini> <INSTALL.md> <icon.png> <out_dir> <version>
"""
import sys
import zipfile
from pathlib import Path

PACK = "eucmod"
PACKAGE_ID = "{6C1F2B8E-4A7D-4E1B-9C3F-2D5E8A9B0C47}"  # fixed so OpenIV sees updates as the same mod

ASSEMBLY = """<?xml version="1.0" encoding="UTF-8"?>
<package version="2.1" id="{id}" target="Five">
  <metadata>
    <name>EUC Mod - Electric Unicycle</name>
    <version>
      <major>{major}</major>
      <minor>{minor}</minor>
    </version>
    <author>
      <displayName>EUC Mod</displayName>
    </author>
    <description><![CDATA[Adds a rideable electric unicycle to GTA V Story Mode.

This package installs the unicycle model and rider animations (add-on pack "{pack}").
You also need Script Hook V, Script Hook V .NET (nightly) and the two files in the
scripts folder of the download - see INSTALL.md.

Story Mode only. Do not use mods in GTA Online.]]></description>
  </metadata>
  <colors>
    <headerBackground useBlackTextColor="False">$FF1E1E1E</headerBackground>
    <iconBackground>$FF2B2B2B</iconBackground>
  </colors>
  <content>
    <add source="{pack}/dlc.rpf">update/x64/dlcpacks/{pack}/dlc.rpf</add>
    <archive path="update/update.rpf" createIfNotExist="False" type="RPF7">
      <xml path="common/data/dlclist.xml">
        <add xpath="/SMandatoryPacksData/Paths">
          <Item>dlcpacks:/{pack}/</Item>
        </add>
      </xml>
    </archive>
  </content>
</package>
"""


def main():
    dlc, dll, ini, install_md, icon, out_dir, version = sys.argv[1:8]
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    major, _, minor = version.partition(".")
    minor = minor or "0"

    oiv = out / "EucMod.oiv"
    with zipfile.ZipFile(oiv, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("assembly.xml", ASSEMBLY.format(id=PACKAGE_ID, major=major, minor=minor, pack=PACK))
        z.write(icon, "icon.png")
        z.write(dlc, f"content/{PACK}/dlc.rpf")
    print(f"wrote {oiv} ({oiv.stat().st_size} bytes)")

    release = out / f"EucMod-{version}.zip"
    with zipfile.ZipFile(release, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(oiv, "EucMod.oiv")
        z.write(dll, "scripts/EucMod.dll")
        z.write(ini, "scripts/EucMod.ini")
        z.write(install_md, "INSTALL.md")
    print(f"wrote {release} ({release.stat().st_size} bytes)")

    for p in (oiv, release):
        with zipfile.ZipFile(p) as z:
            print(f"  {p.name}: " + ", ".join(z.namelist()))


if __name__ == "__main__":
    main()
