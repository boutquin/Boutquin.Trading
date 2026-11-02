#!/usr/bin/env python3
"""Regenerate dependency-source.json from a cold nuget.org restore.

Run `dotnet restore --configfile nuget.ci.config --force-evaluate` with NUGET_PACKAGES pointing at an
empty folder, then `python3 scripts/write-dependency-lock.py --packages <that folder>`. Every resolved
`Boutquin.*` package is recorded with its signed archive hash, signature-excluded content hash,
original payload hashes and source revision.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NUGET_ORG = "https://api.nuget.org/v3/index.json"


def resolved_identities():
    found = {}
    for name in ("src", "tests", "benchmarks"):
        base = ROOT / name
        if not base.is_dir():
            continue
        for project in base.rglob("*.csproj"):
            assets = json.loads((project.parent / "obj/project.assets.json").read_text(encoding="utf-8"))
            for identity, library in assets["libraries"].items():
                if library.get("type") != "project" and identity.startswith("Boutquin."):
                    package, version = identity.rsplit("/", 1)
                    found[package] = version
    return found


def entry(packages, package, version):
    folder = packages / package.lower() / version.lower()
    path = folder / f"{package}.{version}.nupkg".lower()
    raw = path.read_bytes()
    metadata = json.loads((folder / ".nupkg.metadata").read_text(encoding="utf-8"))
    if metadata["source"] != NUGET_ORG:
        raise ValueError(f"{package} {version} did not come from nuget.org: {metadata['source']}")
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if ".signature.p7s" not in names:
            raise ValueError(f"{package} {version} carries no repository signature")
        nuspec = next(n for n in names if n.endswith(".nuspec"))
        repository = next(e for e in ET.fromstring(archive.read(nuspec)).iter() if e.tag.endswith("repository"))
        payload = {n: hashlib.sha256(archive.read(n)).hexdigest() for n in names if n != ".signature.p7s"}
    lower = package.lower()
    return {
        "id": package,
        "version": version,
        "file": f"{package}.{version}.nupkg",
        "sha256": hashlib.sha256(raw).hexdigest(),
        "contentHash": metadata["contentHash"],
        "sourceRepository": repository.get("url"),
        "sourceCommit": repository.get("commit"),
        "packageSource": NUGET_ORG,
        "downloadUrl": f"https://api.nuget.org/v3-flatcontainer/{lower}/{version.lower()}/{lower}.{version.lower()}.nupkg",
        "payloadSha256": payload,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", required=True, type=Path)
    args = parser.parse_args()
    items = [entry(args.packages, package, version) for package, version in sorted(resolved_identities().items())]
    lock = {"schema": 1, "packages": [], "publishedPackages": items}
    (ROOT / "dependency-source.json").write_text(json.dumps(lock, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(items)} locked packages")


if __name__ == "__main__":
    main()
