import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('release_docs', Path(__file__).parents[1] / 'check-release-docs.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)

GOOD_CHANGELOG = '# Changelog\n\n## [Unreleased]\n\n## [2.0.1] — 2026-11-02\n\n### Changed\n- Something.\n'
GOOD_README = ('dotnet add package Boutquin.MarketData.Abstractions --version 2.0.1\n'
               '[docs](https://github.com/boutquin/Boutquin.MarketData/blob/v2.0.1/docs/a.md)\n')


def make(root, changelog=GOOD_CHANGELOG, readme=GOOD_README, doc=''):
    (root / 'docs').mkdir(exist_ok=True)
    (root / 'CHANGELOG.md').write_text(changelog, encoding='utf-8')
    (root / 'README.md').write_text(readme, encoding='utf-8')
    (root / 'docs' / 'a.md').write_text(doc, encoding='utf-8')


class ReleaseDocsTests(unittest.TestCase):
    def errors(self, **kwargs):
        with tempfile.TemporaryDirectory() as d:
            make(Path(d), **kwargs)
            return check.check(Path(d), '2.0.1')

    def test_accepts_dated_release(self):
        self.assertEqual([], self.errors())

    def test_rejects_unreleased_heading(self):
        errors = self.errors(changelog=GOOD_CHANGELOG.replace('2026-11-02', 'Unreleased'))
        self.assertTrue(any('not dated' in e for e in errors))

    def test_rejects_in_preparation_heading(self):
        errors = self.errors(changelog=GOOD_CHANGELOG.replace('2026-11-02', 'dependency refresh in preparation'))
        self.assertTrue(any('not dated' in e for e in errors))

    def test_rejects_missing_release_section(self):
        errors = self.errors(changelog='# Changelog\n\n## [Unreleased]\n')
        self.assertTrue(any('no `## [2.0.1]`' in e for e in errors))

    def test_rejects_unreleased_entries_left_above_release(self):
        errors = self.errors(changelog=GOOD_CHANGELOG.replace('## [Unreleased]\n', '## [Unreleased]\n\n- Late entry.\n'))
        self.assertTrue(any('not empty' in e for e in errors))

    def test_rejects_readme_pinned_to_another_version(self):
        errors = self.errors(readme=GOOD_README.replace('2.0.1', '2.0.0'))
        self.assertTrue(any('--version 2.0.1' in e for e in errors))
        self.assertTrue(any('other source tags' in e for e in errors))

    def test_rejects_stale_wording_in_docs(self):
        errors = self.errors(doc='The stable README links target the future `v2.0.1` snapshot.')
        self.assertTrue(any('stale release wording' in e for e in errors))

    def test_rejects_stale_unreleased_cross_reference(self):
        errors = self.errors(changelog=GOOD_CHANGELOG + '- Old note (superseded in Unreleased by a later change).\n')
        self.assertTrue(any('still refers to an unreleased state' in e for e in errors))

    def test_rejects_non_stable_version(self):
        self.assertEqual(2, check.main(['2.0.0-rc.1']))


if __name__ == '__main__':
    unittest.main()
