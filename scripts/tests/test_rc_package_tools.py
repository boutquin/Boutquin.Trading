"""Executing corruption controls for the package and cold consumer gates."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile


SCRIPTS = Path(__file__).resolve().parents[1]


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), SCRIPTS / (name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


VERIFY = module('verify-rc-packages')
VERSION = '2.0.0-rc.20261027.1'
COMMIT = 'a' * 40


class PackageCorruptionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.feed = Path(self.temporary.name)
        self.sources = {'README.md': b'Committed readme', 'dependency-source.json': b'{"packages":[]}',
                        'Directory.Packages.props': b'<Project><ItemGroup><PackageVersion Include="Boutquin.Domain" Version="[1.0.0]"/></ItemGroup></Project>'}
        for name in VERIFY.IDS:
            dependencies = '' if name.endswith('.Domain') else '<ProjectReference Include="../Boutquin.Trading.Domain/Boutquin.Trading.Domain.csproj"/>'
            dependencies += '<PackageReference Include="Boutquin.Domain"/>'
            self.sources[f"src/{name.rsplit('.', 1)[-1]}/{name}.csproj"] = f'<Project><ItemGroup>{dependencies}</ItemGroup></Project>'.encode()
            versions = '' if name.endswith('.Domain') else f'<dependency id="Boutquin.Trading.Domain" version="[{VERSION}]"/>'
            versions += '<dependency id="Boutquin.Domain" version="[1.0.0]"/>'
            nuspec = f'<package><metadata><id>{name}</id><version>{VERSION}</version><repository url="https://github.com/boutquin/Boutquin.Trading" commit="{COMMIT}"/><license type="expression">Apache-2.0</license><readme>README.md</readme><dependencies><group targetFramework="net10.0">{versions}</group></dependencies></metadata></package>'
            with zipfile.ZipFile(self.feed / f'{name}.{VERSION}.nupkg', 'w') as archive:
                archive.writestr(name + '.nuspec', nuspec)
                archive.writestr('README.md', self.sources['README.md'])
                archive.writestr(f'lib/net10.0/{name}.dll', b'packaged DLL')
                archive.writestr(f'lib/net10.0/{name}.xml', f'<doc><assembly><name>{name}</name></assembly><members><member name="T:Fixture"><summary>Returns an observation.</summary></member></members></doc>')
            with zipfile.ZipFile(self.feed / f'{name}.{VERSION}.snupkg', 'w') as archive:
                archive.writestr(name + '.nuspec', nuspec)
                archive.writestr(f'lib/net10.0/{name}.pdb', b'portable PDB')
        self.addCleanup(patch.stopall)
        patch.object(VERIFY, 'source_bytes', side_effect=lambda commit, path: self.sources[path]).start()
        patch.object(VERIFY.subprocess, 'check_output', side_effect=lambda args, **kwargs: json.dumps({'name': Path(args[2]).stem, 'exportedTypes': 0})).start()

    def verify(self):
        return VERIFY.verify(self.feed, VERSION, COMMIT, self.feed / 'hashes.json', Path('inspector.dll'))

    def mutate(self, member, transform, symbols=False):
        name = VERIFY.IDS[1]
        path = self.feed / f'{name}.{VERSION}.{"snupkg" if symbols else "nupkg"}'
        with zipfile.ZipFile(path) as archive:
            entries = [(entry, archive.read(entry)) for entry in archive.namelist()]
        with zipfile.ZipFile(path, 'w') as archive:
            for entry, content in entries:
                archive.writestr(entry, transform(content) if entry.endswith(member) else content)

    def test_valid_candidate_freezes_and_rechecks_same_identity(self):
        self.assertEqual('PASS', self.verify()['status'])
        self.assertEqual(8, len(self.verify()['sha256']))

    def test_frozen_bytes_cannot_be_replaced(self):
        self.verify()
        self.mutate('README.md', lambda payload: payload + b' changed')
        with self.assertRaisesRegex(ValueError, 'changed after freezing'):
            self.verify()

    def test_missing_archive_is_rejected(self):
        next(self.feed.glob('*.snupkg')).unlink()
        with self.assertRaisesRegex(ValueError, 'exactly 4'):
            self.verify()

    def test_corrupt_metadata_and_ranges_are_rejected(self):
        mutations = [
            (b'Apache-2.0', b'MIT', 'Wrong license'),
            (b'net10.0', b'net9.0', 'framework'),
            (COMMIT.encode(), b'b' * 40, 'source identity'),
            (f'[{VERSION}]'.encode(), f'[{VERSION}, )'.encode(), 'Wrong dependency'),
            (b'[1.0.0]', b'1.0.0', 'Wrong dependency'),
            (b'Boutquin.Domain', b'Unknown.Domain', 'unexpected dependencies'),
            (b'<id>Boutquin.Trading.Application</id>', b'<id>Unknown</id>', 'nuspec identity'),
        ]
        for old, new, message in mutations:
            with self.subTest(message=message):
                # Each mutation starts with no frozen identity and is restored afterwards.
                self.mutate('.nuspec', lambda payload: payload.replace(old, new))
                with self.assertRaisesRegex(ValueError, message):
                    self.verify()
                (self.feed / 'hashes.json').unlink(missing_ok=True)
                self.mutate('.nuspec', lambda payload: payload.replace(new, old))

    def test_symbol_source_mismatch_is_rejected(self):
        self.mutate('.nuspec', lambda data: data.replace(COMMIT.encode(), b'b' * 40), symbols=True)
        with self.assertRaisesRegex(ValueError, 'symbol source'):
            self.verify()

    def test_readme_must_equal_source_bytes(self):
        self.mutate('README.md', lambda payload: payload + b' changed')
        with self.assertRaisesRegex(ValueError, 'README differs'):
            self.verify()

    def test_private_xml_reference_is_rejected(self):
        self.mutate('.xml', lambda payload: payload.replace(b'Returns an observation.', b'specs/private.md'))
        with self.assertRaisesRegex(ValueError, 'XML documentation'):
            self.verify()

    def test_extra_dll_is_rejected(self):
        path = self.feed / f'{VERIFY.IDS[1]}.{VERSION}.nupkg'
        with zipfile.ZipFile(path, 'a') as archive:
            archive.writestr('lib/net9.0/Unexpected.dll', b'stale')
            archive.writestr('lib/net9.0/Unexpected.xml', '<doc><assembly><name>Unexpected</name></assembly><members><member name="T:Unexpected"><summary>Unexpected API.</summary></member></members></doc>')
        with self.assertRaisesRegex(ValueError, 'DLL inventory'):
            self.verify()

    def add_dependency(self, pinned, version='[2.0.1]'):
        if pinned:
            self.sources['Directory.Packages.props'] = (
                b'<Project><ItemGroup><PackageVersion Include="Boutquin.Domain" Version="[1.0.0]"/>'
                b'<PackageVersion Include="Boutquin.Storage.Domain" Version="[2.0.1]"/>'
                b'<PackageVersion Include="Polly.Core" Version="8.9.0"/></ItemGroup></Project>')
        self.mutate('.nuspec', lambda content: content.replace(
            b'</group>', f'<dependency id="Boutquin.Storage.Domain" version="{version}"/></group>'.encode()))

    def test_exact_transitive_pin_from_the_central_pins_is_accepted(self):
        self.add_dependency(True)
        self.assertEqual('PASS', self.verify()['status'])

    def test_extra_dependency_outside_the_central_pins_is_rejected(self):
        self.add_dependency(False)
        with self.assertRaisesRegex(ValueError, 'Missing or unexpected dependencies'):
            self.verify()

    def test_transitive_pin_at_the_wrong_version_is_rejected(self):
        self.add_dependency(True, '[2.0.9]')
        with self.assertRaisesRegex(ValueError, 'Wrong transitive pin'):
            self.verify()

    def test_build_metadata_identity_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'exact stable or numeric RC identity'):
            VERIFY.verify(self.feed, VERSION + '+metadata', COMMIT, self.feed / 'hashes.json', Path('inspector'))


if __name__ == '__main__':
    unittest.main()
