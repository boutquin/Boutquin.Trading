import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("release", Path(__file__).parents[1] / "verify-release-assets.py")
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)

class ReleaseAssetTests(unittest.TestCase):
    def test_frozen_bytes_source_tag_and_manifest_are_required(self):
        with tempfile.TemporaryDirectory() as d:
            feed = Path(d)
            package = feed / "example.nupkg"
            package.write_bytes(b"qualified")
            identity = {"version": "2.3.1-rc.20261027.1", "sourceCommit": "a" * 40,
                        "sha256": {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}}
            manifest = feed / "package-hashes.json"
            manifest.write_text(json.dumps(identity))
            digest = hashlib.sha256(manifest.read_bytes()).hexdigest()
            tag = "v" + identity["version"]
            self.assertEqual(manifest, check.validate(feed, tag, digest, "a" * 40))
            for invalid_tag, invalid_hash, invalid_commit in [("v2.3.2", digest, "a" * 40),
                    (tag, "b" * 64, "a" * 40), (tag, digest, "b" * 40)]:
                with self.assertRaises(ValueError):
                    check.validate(feed, invalid_tag, invalid_hash, invalid_commit)
            package.write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "Changed release archive"):
                check.validate(feed, tag, digest, "a" * 40)

    def test_stable_and_rc_identity_cross_product(self):
        for version in ('2.3.1', '2.3.1-rc.20261027.4'):
            with self.subTest(version=version), tempfile.TemporaryDirectory() as d:
                feed = Path(d)
                package = feed / 'example.nupkg'
                package.write_bytes(b'qualified')
                identity = {'version': version, 'sourceCommit': 'a' * 40,
                            'sha256': {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}}
                manifest = feed / 'package-hashes.json'
                manifest.write_text(json.dumps(identity))
                digest = hashlib.sha256(manifest.read_bytes()).hexdigest()
                self.assertEqual(manifest, check.validate(feed, 'v' + version, digest, 'a' * 40))
                for tag in ('v2.3.1', 'v2.3.1-rc.20261027.4', 'v2.3.2', 'v2.3.2-rc.20261030.1', 'v2.4.0', 'v1.0.0', 'v2.3.2-beta.1', 'v2.3.1-beta.1',
                            'v2.3.1+metadata', 'v2.3.1-rc.', 'v2.3.1-rc.1/../../x', '2.3.1'):
                    if tag != 'v' + version:
                        with self.assertRaises(ValueError):
                            check.validate(feed, tag, digest, 'a' * 40)
                for invalid_hash, invalid_commit in (('b' * 64, 'a' * 40), (digest, 'b' * 40)):
                    with self.assertRaises(ValueError):
                        check.validate(feed, 'v' + version, invalid_hash, invalid_commit)
                package.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError, 'Changed release archive'):
                    check.validate(feed, 'v' + version, digest, 'a' * 40)

class PackagedReadmeTests(unittest.TestCase):
    def test_file_links_are_absolute_for_nuget_gallery(self):
        import re
        readme = (Path(__file__).parents[2] / 'README.md').read_text(encoding='utf-8')
        targets = re.findall(r'\]\(([^)]+)\)', readme)
        self.assertTrue(targets)
        self.assertEqual([], [target for target in targets if not target.startswith(('https://', '#'))])
