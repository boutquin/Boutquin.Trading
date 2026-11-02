#!/usr/bin/env python3
"""Build one immutable Trading candidate from a clean committed public source clone."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
IDS = tuple('Boutquin.Trading.' + suffix for suffix in ('Domain', 'Application', 'DataAccess', 'Recipes'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--version', required=True, help='Never previously accepted stable or numeric RC version')
    args = parser.parse_args()
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-rc\.[0-9]+(?:\.[0-9]+)*)?', args.version):
        parser.error('Expected an exact stable or numeric RC version')
    if not re.fullmatch('[0-9a-f]{40}', args.source):
        parser.error('source must be a full committed SHA')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    version = args.version
    source, feed, cache, http = (output / name for name in ('source', 'packages', 'build-cache', 'http-cache'))
    for path in (feed, cache, http):
        path.mkdir()
    env = dict(os.environ, NUGET_PACKAGES=str(cache), NUGET_HTTP_CACHE_PATH=str(http), CI='true')
    commands = []

    def run(command, cwd=ROOT):
        log = output / f'{len(commands):02d}-{command[0].replace(os.sep, "_")}.txt'
        with log.open('x') as stream:
            result = subprocess.run(command, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT)
        commands.append(dict(command=command, cwd=str(cwd), exitCode=result.returncode, log=log.name))
        (output / 'commands.json').write_text(json.dumps(commands, indent=2) + '\n')
        if result.returncode:
            raise SystemExit(f'Failed: {command}; see {log}')

    run(['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(source)])
    run(['git', 'remote', 'set-url', 'origin', 'git@github.com:boutquin/Boutquin.Trading.git'], source)
    run(['git', 'checkout', '--detach', args.source], source)
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=source).strip():
        raise SystemExit('Candidate source clone is not clean')
    run(['python3', 'scripts/check-runner-dependencies.py'], source)
    solution = 'Boutquin.Trading.slnx'
    run(['dotnet', 'restore', solution, '--configfile', 'nuget.ci.config', '--no-http-cache', '--locked-mode', '-p:_WorkloadLibraryPacksFolder=' + str(output / 'absent-sdk-feed')], source)
    run(['python3', 'scripts/check-runner-dependencies.py', '--packages', str(cache)], source)
    run(['dotnet', 'build', solution, '-c', 'Release', '--no-restore', '--no-incremental', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false', '-p:GeneratePackageOnBuild=false', '-p:MinVerVersionOverride=' + version], source)
    run(['dotnet', 'test', solution, '-c', 'Release', '--no-build', '--no-restore', '--logger', 'trx', '--results-directory', str(output / 'source-trx')], source)
    run(['python3', 'scripts/verify-dependency-identities.py', '--packages', str(cache), '--outputs', str(source), '--report', str(output / 'dependency-identities.json')], source)
    for name in IDS:
        run(['dotnet', 'pack', f'src/{name.rsplit(".", 1)[-1]}/{name}.csproj', '-c', 'Release', '--no-build', '--no-restore', '-p:MinVerVersionOverride=' + version, '--output', str(feed)], source)
    run(['dotnet', 'build', 'scripts/package-inspector/PackageInspector.csproj', '-c', 'Release'], source)
    run(['python3', 'scripts/verify-rc-packages.py', str(feed), version, args.source, '--manifest', str(output / 'package-hashes.json'), '--report', str(output / 'package-inspection.json')], source)
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=source).strip():
        raise SystemExit('Candidate build modified tracked source or introduced untracked source')
    with (output / 'candidate.json').open('x') as stream:
        json.dump(dict(status='PASS', version=version, sourceCommit=args.source, source=str(source), packages=str(feed)), stream, indent=2)
        stream.write('\n')
    print(f'PASS: {version}; candidate frozen at {output}')


if __name__ == '__main__':
    main()
