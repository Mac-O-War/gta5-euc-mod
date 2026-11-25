"""Pack exported assets into an add-on DLC pack (dlc.rpf) for GTA V Legacy.

Layout produced:
  dlc.rpf
    content.xml
    setup2.xml
    x64/eucmod.rpf          <- streaming archive (RPF_FILE)
      euc_offroad.ydr
      euc_offroad.ytyp      <- also registered via DLC_ITYP_REQUEST
      euc_rider.ycd         <- rider clip dictionary (lean / brake / air)

Run with a Python that has pymateria (Blender's bundled Python + Sollumz site-packages):
  python tools/build_dlc.py <assets_dir> <out_dlc_rpf>
"""
import sys
from pathlib import Path

from pymateria.rpf7 import (
    PackFile,
    PackFileEntryArchive,
    PackFileEntryDirectory,
    PackFileEntryFile,
    PackFileEntryResource,
)

PACK = "eucmod"
DEVICE = f"dlc_{PACK}"
CHANGESET = f"{PACK.upper()}_AUTOGEN"
STREAM_RPF = f"{DEVICE}:/%PLATFORM%/{PACK}.rpf"
RESOURCE_EXTS = {".ydr", ".ytd", ".yft", ".ytyp", ".ybn", ".ydd", ".ycd"}


def content_xml(ytyps: list[str]) -> str:
    items = [
        f"""    <Item>
      <filename>{STREAM_RPF}</filename>
      <fileType>RPF_FILE</fileType>
      <overlay value="false" />
      <disabled value="true" />
      <persistent value="true" />
    </Item>"""
    ]
    for y in ytyps:
        items.append(f"""    <Item>
      <filename>{STREAM_RPF}/{y}</filename>
      <fileType>DLC_ITYP_REQUEST</fileType>
      <overlay value="false" />
      <disabled value="true" />
      <persistent value="true" />
    </Item>""")
    enable = "\n".join(f"        <Item>{STREAM_RPF}{'/' + y if y else ''}</Item>" for y in [""] + ytyps)
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<CDataFileMgr__ContentsOfDataFileXml>
  <disabledFiles />
  <includedXmlFiles />
  <includedDataFiles />
  <dataFiles>
{chr(10).join(items)}
  </dataFiles>
  <contentChangeSets>
    <Item>
      <changeSetName>{CHANGESET}</changeSetName>
      <mapChangeSetData />
      <filesToInvalidate />
      <filesToDisable />
      <filesToEnable>
{enable}
      </filesToEnable>
      <txdToLoad />
      <txdToUnload />
      <residentResources />
      <unregisterResources />
    </Item>
  </contentChangeSets>
  <patchFiles />
</CDataFileMgr__ContentsOfDataFileXml>
"""


SETUP2_XML = f"""<?xml version="1.0" encoding="UTF-8"?>
<SSetupData>
  <deviceName>{DEVICE}</deviceName>
  <datFile>content.xml</datFile>
  <timeStamp>11/25/2026 00:00:00</timeStamp>
  <nameHash>{PACK}</nameHash>
  <contentChangeSets />
  <contentChangeSetGroups>
    <Item>
      <NameHash>GROUP_STARTUP</NameHash>
      <ContentChangeSets>
        <Item>{CHANGESET}</Item>
      </ContentChangeSets>
    </Item>
  </contentChangeSetGroups>
  <startupScript />
  <scriptCallstackSize value="0" />
  <type>EXTRACONTENT_COMPAT_PACK</type>
  <order value="31" />
  <minorOrder value="0" />
  <isLevelPack value="false" />
  <dependencyPackHash />
  <requiredVersion />
  <subPackCount value="0" />
</SSetupData>
"""


def build(assets_dir: Path, out: Path) -> None:
    assets = sorted(p for p in assets_dir.iterdir() if p.suffix.lower() in RESOURCE_EXTS)
    if not assets:
        sys.exit(f"no game assets found in {assets_dir}")
    ytyps = [p.name for p in assets if p.suffix.lower() == ".ytyp"]

    # Build the inner streaming archive first, serialise it, then embed it in dlc.rpf.
    stream = PackFile.create()
    for p in assets:
        stream.add_entry(PackFileEntryResource.create(stream, p.name, p), True)
    stream_bytes = _to_bytes(stream)

    dlc = PackFile.create()
    dlc.add_entry(PackFileEntryFile.create(dlc, "content.xml", content_xml(ytyps).encode("utf-8")), True)
    dlc.add_entry(PackFileEntryFile.create(dlc, "setup2.xml", SETUP2_XML.encode("utf-8")), True)
    dlc.add_entry(PackFileEntryDirectory(dlc, "x64"), True)
    dlc.add_entry(PackFileEntryArchive.create(dlc, f"x64/{PACK}.rpf", f"{PACK}.rpf", stream_bytes), True)

    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "wb") as f:
        PackFile.export_rpf(dlc, f)
    print(f"wrote {out} ({out.stat().st_size} bytes)")


def _to_bytes(pf: PackFile) -> bytes:
    import io
    buf = io.BytesIO()
    PackFile.export_rpf(pf, buf)
    return buf.getvalue()


def verify(path: Path) -> None:
    """Re-open the archive with the reader and list every entry, including the nested archive."""
    pf = PackFile.import_rpf(str(path))
    for e in pf.get_all_entries():
        entry = e.entry
        extra = ""
        if isinstance(entry, PackFileEntryResource):
            extra = f" resource v{entry.resource_version}, {entry.size} bytes"
        elif isinstance(entry, PackFileEntryFile):
            extra = f" {entry.size} bytes"
        print(f"  {e.full_path}  [{entry.type.name}]{extra}")


if __name__ == "__main__":
    assets_dir, out = Path(sys.argv[1]), Path(sys.argv[2])
    build(assets_dir, out)
    verify(out)
