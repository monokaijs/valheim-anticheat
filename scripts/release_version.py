#!/usr/bin/env python3
"""Keep the assembly and BepInEx plugin versions in sync for manual releases."""

import argparse
import re
from pathlib import Path


PROJECT_VERSION = re.compile(r"(<Version>)(\d+\.\d+\.\d+)(</Version>)")
PLUGIN_VERSION = re.compile(
    r'(\[BepInPlugin\("dev\.monokaijs\.valheim\.anticheat",\s*"Valheim Anticheat",\s*")(\d+\.\d+\.\d+)("\)\])'
)


def files(root: Path) -> tuple[Path, Path]:
    return root / "ValheimAnticheat.csproj", root / "src/AnticheatPlugin.cs"


def read_version(root: Path) -> str:
    project, plugin = files(root)
    project_match = PROJECT_VERSION.search(project.read_text())
    plugin_match = PLUGIN_VERSION.search(plugin.read_text())
    if project_match is None or plugin_match is None:
        raise ValueError("Could not find both project and plugin versions")
    if project_match.group(2) != plugin_match.group(2):
        raise ValueError("Project and plugin versions disagree")
    return project_match.group(2)


def next_version(current: str, mode: str) -> str:
    major, minor, patch = (int(part) for part in current.split("."))
    if mode == "current":
        return current
    if mode == "patch":
        return f"{major}.{minor}.{patch + 1}"
    if mode == "minor":
        return f"{major}.{minor + 1}.0"
    if mode == "major":
        return f"{major + 1}.0.0"
    raise ValueError(f"Unsupported release mode: {mode}")


def apply_version(root: Path, mode: str) -> str:
    current = read_version(root)
    version = next_version(current, mode)
    if version != current:
        project, plugin = files(root)
        project.write_text(PROJECT_VERSION.sub(lambda match: match.group(1) + version + match.group(3), project.read_text(), count=1))
        plugin.write_text(PLUGIN_VERSION.sub(lambda match: match.group(1) + version + match.group(3), plugin.read_text(), count=1))
    return version


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("current", "patch", "minor", "major"))
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    print(apply_version(args.root, args.mode))
