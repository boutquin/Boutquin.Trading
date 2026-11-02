import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile

spec = importlib.util.spec_from_file_location('prepared', Path(__file__).parents[1] / 'prepared-dependencies.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)


class PreparedDependenciesTests(unittest.TestCase):
    def test_frozen_input_and_exact_mappings(self):
        with tempfile.TemporaryDirectory() as d, patch.dict(os.environ, {'BOUTQUIN_PREPARED_FEED': d}):
            feed = Path(d)
            archive = feed / 'package.nupkg'
            archive.write_bytes(b'original')
            item = {'id': 'Boutquin.MarketData.Abstractions', 'file': archive.name,
                    'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()}
            config = feed / 'nuget.config'
            check.write_config(config, [item])
            xml = ET.parse(config)
            mappings = {s.get('key'): [p.get('pattern') for p in s] for s in xml.findall('.//packageSource')}
            self.assertEqual({'prepared': [item['id']], 'nuget.org': ['*']}, mappings)
            self.assertEqual(str(feed.resolve()), check.source(item))
            archive.write_bytes(b'substituted')
            with self.assertRaisesRegex(ValueError, 'Prepared archive bytes changed'):
                check.verify(item, archive)

    def test_public_signing_requires_verifier_and_exact_payload(self):
        with tempfile.TemporaryDirectory() as d, patch.dict(os.environ, {}, clear=True):
            path = Path(d) / 'package.nupkg'
            item = {'id': 'example', 'sha256': '0' * 64,
                    'payloadSha256': {'payload': hashlib.sha256(b'original').hexdigest()}}
            for signed, payload in ((False, b'original'), (True, b'changed'), (True, b'original')):
                with zipfile.ZipFile(path, 'w') as archive:
                    archive.writestr('payload', payload)
                    if signed:
                        archive.writestr('.signature.p7s', b'test-signature')
                with patch.object(check.subprocess, 'run') as verifier:
                    if signed and payload == b'original':
                        self.assertEqual(path.read_bytes(), check.verify(item, path))
                    else:
                        with self.assertRaises(ValueError):
                            check.verify(item, path)
                    verifier.assert_called_once()
                with patch.object(check.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, 'verify')):
                    with self.assertRaises(subprocess.CalledProcessError):
                        check.verify(item, path)
