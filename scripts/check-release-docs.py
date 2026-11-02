#!/usr/bin/env python3
"""Fail when the public documentation does not describe the release being cut.

Usage: check-release-docs.py <version> [--root PATH]

Checks, for an exact stable version such as 2.0.1:
  * CHANGELOG.md has a dated section `## [<version>] — YYYY-MM-DD`, not "Unreleased" or "in preparation";
  * an empty `## [Unreleased]` section sits above it;
  * README.md installs the exact version and links to the matching `v<version>` source tag;
  * public docs do not describe the release as a future or candidate state.
"""
import argparse
import re
import sys
from pathlib import Path

STALE_PHRASES = (
    'in preparation',
    'prepared for release',
    'future `v',
    'not the qualified public',
    'uses published storage 2.0.0 archives',
)


def check(root, version):
    errors = []
    changelog = (root / 'CHANGELOG.md').read_text(encoding='utf-8')
    heading = re.search(r'^## \[' + re.escape(version) + r'\][^\n]*$', changelog, re.M)
    if heading is None:
        errors.append(f'CHANGELOG.md has no `## [{version}]` section')
    elif not re.fullmatch(r'## \[' + re.escape(version) + r'\] — \d{4}-\d{2}-\d{2}', heading.group(0)):
        errors.append(f'CHANGELOG.md heading is not dated (`## [{version}] — YYYY-MM-DD`): {heading.group(0)}')
    unreleased = re.search(r'^## \[Unreleased\]\s*$', changelog, re.M)
    if unreleased is None:
        errors.append('CHANGELOG.md lacks an `## [Unreleased]` section for the next change')
    elif heading is not None and unreleased.start() > heading.start():
        errors.append('CHANGELOG.md lists `[Unreleased]` below the release section')
    elif heading is not None and changelog[unreleased.end():heading.start()].strip():
        errors.append('CHANGELOG.md `[Unreleased]` is not empty: move its entries into the release section')

    for number, line in enumerate(changelog.splitlines(), 1):
        if re.search('unreleased', line, re.I) and line.strip() != '## [Unreleased]':
            errors.append(f'CHANGELOG.md:{number} still refers to an unreleased state: {line.strip()[:80]}')

    readme = (root / 'README.md').read_text(encoding='utf-8')
    if f'--version {version}' not in readme:
        errors.append(f'README.md does not install --version {version}')
    stale_links = sorted(set(re.findall(r'/blob/(v[0-9][^/]*)/', readme)) - {f'v{version}'})
    if stale_links:
        errors.append('README.md links to other source tags: ' + ', '.join(stale_links))

    for path in sorted((root / 'docs').glob('*.md')) + [root / 'README.md']:
        text = path.read_text(encoding='utf-8').lower()
        for phrase in STALE_PHRASES:
            if phrase in text:
                errors.append(f'{path.relative_to(root)} contains stale release wording: "{phrase}"')
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('version')
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args(argv)
    if not re.fullmatch(r'\d+\.\d+\.\d+', args.version):
        print('FAIL: version must be an exact stable version such as 2.0.1', file=sys.stderr)
        return 2
    errors = check(args.root, args.version)
    for error in errors:
        print('FAIL: ' + error, file=sys.stderr)
    if not errors:
        print(f'PASS: release documentation describes {args.version}')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
