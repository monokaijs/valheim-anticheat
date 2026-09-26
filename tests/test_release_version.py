import tempfile
import unittest
from pathlib import Path

from scripts.release_version import apply_version, next_version, read_version


class ReleaseVersionTests(unittest.TestCase):
    def test_semantic_bumps(self):
        self.assertEqual(next_version("0.0.1", "current"), "0.0.1")
        self.assertEqual(next_version("0.0.1", "patch"), "0.0.2")
        self.assertEqual(next_version("0.0.1", "minor"), "0.1.0")
        self.assertEqual(next_version("0.0.1", "major"), "1.0.0")

    def test_updates_all_version_sites(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "ValheimAnticheat.csproj").write_text("<Version>0.0.1</Version>\n")
            (root / "src/AnticheatPlugin.cs").write_text(
                '[BepInPlugin("dev.monokaijs.valheim.anticheat", "Valheim Anticheat", "0.0.1")]\n'
            )
            (root / "thunderstore.toml").write_text('versionNumber = "0.0.1"\n')
            self.assertEqual(apply_version(root, "minor"), "0.1.0")
            self.assertEqual(read_version(root), "0.1.0")


if __name__ == "__main__":
    unittest.main()
