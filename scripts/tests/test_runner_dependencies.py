"""Dependency verification rejects changed provenance, with project references as a control."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location('checker', Path(__file__).parents[1] / 'check-runner-dependencies.py')
CHECKER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKER)

NUGET_ORG = 'https://api.nuget.org/v3/index.json'
CONFIG = ('<configuration><packageSources><clear /><add key="nuget.org" value="' + NUGET_ORG + '" /></packageSources>'
          '<packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource>'
          '</packageSourceMapping></configuration>')


class RunnerDependencyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.cache = self.root / 'cache'
        self.id = 'Boutquin.Numerics'
        self.version = '2.0.0'
        self.commit = 'a' * 40
        folder = self.cache / self.id.lower() / self.version
        folder.mkdir(parents=True)
        self.archive_path = folder / f'{self.id.lower()}.{self.version}.nupkg'
        with zipfile.ZipFile(self.archive_path, 'w') as z:
            z.writestr('lib/a.dll', 'dll')
            z.writestr('x.nuspec', f'<package><metadata><id>{self.id}</id><version>{self.version}</version>'
                                   f'<repository url="repo" commit="{self.commit}" /></metadata></package>')
            z.writestr('.signature.p7s', 'signature')
        raw = self.archive_path.read_bytes()
        self.sha512 = base64.b64encode(hashlib.sha512(raw).digest()).decode()
        (folder / f'{self.id.lower()}.{self.version}.nupkg.sha512').write_text(self.sha512)
        self.metadata = folder / '.nupkg.metadata'
        self.metadata.write_text(json.dumps(dict(source=NUGET_ORG, contentHash='content-hash')))
        with zipfile.ZipFile(self.archive_path) as z:
            payload = {n: hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist() if n != '.signature.p7s'}
        self.item = dict(id=self.id, version=self.version, file=self.archive_path.name, sha256=hashlib.sha256(raw).hexdigest(),
                         contentHash='content-hash', sourceRepository='repo', sourceCommit=self.commit,
                         packageSource=NUGET_ORG, payloadSha256=payload)
        self.lock = dict(schema=1, packages=[], publishedPackages=[self.item])
        self.save_lock()
        for name in ('nuget.config', 'nuget.ci.config'):
            (self.root / name).write_text(CONFIG)
        self.props = self.root / 'Directory.Packages.props'
        self.props.write_text(f'<Project><ItemGroup><PackageVersion Include="{self.id}" Version="[{self.version}]" />'
                              '</ItemGroup></Project>')
        project = self.root / 'src' / 'Test'
        (project / 'obj').mkdir(parents=True)
        (project / 'Test.csproj').write_text('<Project/>')
        self.assets = project / 'obj/project.assets.json'
        self.libraries = {f'{self.id}/{self.version}': {'type': 'package'},
                          'Boutquin.Trading.Core/1.0.0': {'type': 'project'}}
        self.save_assets()
        for target, value in (('ROOT', self.root),):
            patcher = patch.object(CHECKER, target, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        verifier = patch.object(CHECKER.runpy, 'run_path', return_value={'verify': self.fake_verify})
        verifier.start()
        self.addCleanup(verifier.stop)

    @staticmethod
    def fake_verify(item, path):
        """Stand in for the signature verifier; payload equality is the property under test."""
        with zipfile.ZipFile(path) as archive:
            names = archive.namelist()
            if '.signature.p7s' not in names:
                raise ValueError('Invalid signed archive inventory')
            actual = {n: hashlib.sha256(archive.read(n)).hexdigest() for n in names if n != '.signature.p7s'}
        if actual != item['payloadSha256']:
            raise ValueError('Signed package payload differs from sealed input: ' + item['id'])
        return path.read_bytes()

    def save_lock(self):
        (self.root / 'dependency-source.json').write_text(json.dumps(self.lock))

    def save_assets(self):
        self.assets.write_text(json.dumps(dict(libraries=self.libraries)))

    def test_locked_closure_passes_with_project_reference_control(self):
        CHECKER.verify()
        CHECKER.verify(self.cache)

    def test_unlocked_ecosystem_package_is_rejected(self):
        self.libraries['Boutquin.Trading.Core/1.0.0']['type'] = 'package'
        self.save_assets()
        with self.assertRaisesRegex(ValueError, 'Unlocked'):
            CHECKER.verify(self.cache)

    def test_private_feed_packages_are_rejected(self):
        self.lock['packages'] = [self.item]
        self.save_lock()
        with self.assertRaisesRegex(ValueError, 'Private feed'):
            CHECKER.verify()

    def test_wrong_central_pin_rejected(self):
        self.props.write_text(self.props.read_text().replace(self.version, '2.0.1'))
        with self.assertRaisesRegex(ValueError, 'Central pin'):
            CHECKER.verify()

    def test_unused_central_pin_is_allowed_but_a_later_use_is_not(self):
        self.props.write_text(self.props.read_text().replace(
            '</ItemGroup>', '<PackageVersion Include="Boutquin.MarketData.Testing" Version="[2.0.1]" /></ItemGroup>'))
        CHECKER.verify(self.cache)
        self.libraries['Boutquin.MarketData.Testing/2.0.1'] = {'type': 'package'}
        self.save_assets()
        with self.assertRaisesRegex(ValueError, 'Unlocked'):
            CHECKER.verify(self.cache)

    def test_wrong_restored_version_rejected(self):
        self.libraries = {f'{self.id}/2.0.9': {'type': 'package'}}
        self.save_assets()
        with self.assertRaisesRegex(ValueError, 'Unexpected resolved package version'):
            CHECKER.verify(self.cache)

    def test_non_nuget_org_source_in_lock_rejected(self):
        self.item['packageSource'] = '/Users/someone/.local-feed'
        self.save_lock()
        with self.assertRaisesRegex(ValueError, 'nuget.org'):
            CHECKER.verify()

    def test_local_feed_in_nuget_config_rejected(self):
        (self.root / 'nuget.config').write_text(CONFIG.replace(
            '<clear />', '<clear /><add key="local" value="/Users/someone/.local-feed" />'))
        with self.assertRaisesRegex(ValueError, 'only nuget.org'):
            CHECKER.verify()

    def test_wrong_restore_source_rejected(self):
        self.metadata.write_text(json.dumps(dict(source='/Users/someone/.local-feed', contentHash='content-hash')))
        with self.assertRaisesRegex(ValueError, 'source'):
            CHECKER.verify(self.cache)

    def test_wrong_restored_content_hash_rejected(self):
        self.metadata.write_text(json.dumps(dict(source=NUGET_ORG, contentHash='other')))
        with self.assertRaisesRegex(ValueError, 'content mismatch'):
            CHECKER.verify(self.cache)

    def test_changed_archive_bytes_rejected(self):
        with zipfile.ZipFile(self.archive_path, 'a') as z:
            z.writestr('lib/extra.dll', 'substituted')
        with self.assertRaisesRegex(ValueError, 'payload differs'):
            CHECKER.verify(self.cache)

    def test_unsigned_archive_rejected(self):
        with zipfile.ZipFile(self.archive_path) as source:
            entries = {n: source.read(n) for n in source.namelist() if n != '.signature.p7s'}
        with zipfile.ZipFile(self.archive_path, 'w') as z:
            for name, data in entries.items():
                z.writestr(name, data)
        with self.assertRaisesRegex(ValueError, 'signed archive'):
            CHECKER.verify(self.cache)

    def test_wrong_source_commit_rejected(self):
        self.item['sourceCommit'] = 'b' * 40
        self.save_lock()
        with self.assertRaisesRegex(ValueError, 'source revision'):
            CHECKER.verify(self.cache)


if __name__ == '__main__':
    unittest.main()
