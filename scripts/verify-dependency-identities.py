#!/usr/bin/env python3
"""Verify locked dependency DLL identities and that built runtime copies are the packaged bytes."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
INSPECTOR = ROOT / 'scripts/package-inspector/bin/Release/net10.0/PackageInspector.dll'


def packaged_dlls(archive, item):
    """Runtime assemblies the package ships for the net10.0 target."""
    return [n for n in archive.namelist() if n.startswith('lib/net10.0/') and n.endswith('.dll')]


def verify(cache, outputs):
    subprocess.run(['dotnet', 'build', str(ROOT / 'scripts/package-inspector/PackageInspector.csproj'),
                    '-c', 'Release', '--nologo'], check=True, stdout=subprocess.DEVNULL)
    lock = json.loads((ROOT / 'dependency-source.json').read_text(encoding='utf-8'))
    identities, hashes = [], {}
    with tempfile.TemporaryDirectory(prefix='dependency-identity-') as temp:
        for item in lock['publishedPackages']:
            path = cache / item['id'].lower() / item['version'].lower() / f"{item['id']}.{item['version']}.nupkg".lower()
            if not path.exists():
                continue  # Locked but not restored by this solution (build tools, test-only packages).
            with zipfile.ZipFile(path) as archive:
                for name in packaged_dlls(archive, item):
                    content = archive.read(name)
                    extracted = Path(temp) / Path(name).name
                    extracted.write_bytes(content)
                    identity = json.loads(subprocess.check_output(
                        ['dotnet', str(INSPECTOR), '--assembly-only', str(extracted), item['version'], item['sourceCommit']],
                        text=True))
                    digest = hashlib.sha256(content).hexdigest()
                    hashes[extracted.name] = digest
                    identities.append({**identity, 'package': item['id'], 'version': item['version'],
                                       'sha256': digest, 'archiveEntry': name})
    copies = []
    for path in outputs.rglob('Boutquin.*.dll'):
        relative = path.relative_to(outputs)
        if 'bin' not in relative.parts or 'Release' not in relative.parts or 'artifacts' in relative.parts or 'obj' in relative.parts:
            continue
        if path.name not in hashes:
            continue  # This repository's own assemblies.
        if hashlib.sha256(path.read_bytes()).hexdigest() != hashes[path.name]:
            raise ValueError('Runtime copy differs from the packaged DLL: ' + str(relative))
        copies.append(str(relative))
    if not identities or not copies:
        raise ValueError('No locked dependency DLLs or runtime copies were inspected')
    return {'status': 'PASS', 'identities': identities, 'runtimeCopies': sorted(copies)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', required=True, type=Path)
    parser.add_argument('--outputs', required=True, type=Path)
    parser.add_argument('--report', required=True, type=Path)
    args = parser.parse_args()
    report = verify(args.packages.resolve(), args.outputs.resolve())
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(f"PASS: {len(report['identities'])} locked dependency DLLs match their pinned identity; "
          f"{len(report['runtimeCopies'])} runtime copies are the packaged bytes")


if __name__ == '__main__':
    main()
