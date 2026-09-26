#!/usr/bin/env python3
"""Build the installable GitHub release archive from an already compiled DLL."""

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from release_version import read_version


root = Path(__file__).resolve().parents[1]
version = read_version(root)
dll = root / "bin/Release/netstandard2.1/ValheimAnticheat.dll"
if not dll.is_file():
    raise SystemExit("Release DLL is missing; run dotnet build -c Release first")

archive = root / "dist" / f"ValheimAnticheat-{version}.zip"
archive.parent.mkdir(parents=True, exist_ok=True)
with ZipFile(archive, "w", compression=ZIP_DEFLATED) as output:
    for source, name in (
        (dll, "ValheimAnticheat/ValheimAnticheat.dll"),
        (root / "README.md", "README.md"),
        (root / "assets/icon.png", "icon.png"),
        (root / "docs/anti-cheat-approaches.md", "docs/anti-cheat-approaches.md"),
    ):
        output.write(source, name)
with ZipFile(archive) as output:
    if output.testzip() is not None:
        raise SystemExit("Release archive failed CRC check")
print(archive)
