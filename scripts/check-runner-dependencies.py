#!/usr/bin/env python3
"""Verify the signed nuget.org dependency closure: central pins, archive bytes and restore provenance."""

import argparse
import base64
import hashlib
import json
from pathlib import Path
import runpy
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NUGET_ORG = "https://api.nuget.org/v3/index.json"
FAMILY = ("Boutquin.",)


def verify_mappings():
    """Both NuGet configurations may name only nuget.org, mapped for every package."""
    for name in ("nuget.config", "nuget.ci.config"):
        config = ET.parse(ROOT / name)
        mappings = {e.get("key"): {p.get("pattern") for p in e.findall("package")}
                    for e in config.findall(".//packageSourceMapping/packageSource")}
        sources = {e.get("key"): e.get("value") for e in config.findall(".//packageSources/add")}
        if mappings != {"nuget.org": {"*"}} or sources != {"nuget.org": NUGET_ORG}:
            raise ValueError(f"{name} must use only nuget.org with a catch-all source mapping")


def verify_archive(item, path):
    """Check the signed archive: valid repository signature, original payload, identity and source commit."""
    content = runpy.run_path(str(ROOT / "scripts/prepared-dependencies.py"))["verify"](item, path)
    with zipfile.ZipFile(path) as archive:
        names = [n for n in archive.namelist() if n.endswith(".nuspec")]
        if len(names) != 1:
            raise ValueError("Expected exactly one package nuspec")
        elements = {el.tag.rsplit("}", 1)[-1]: el for el in ET.fromstring(archive.read(names[0])).iter()}
    if (elements["id"].text, elements["version"].text) != (item["id"], item["version"]):
        raise ValueError(f"Package identity mismatch: {item['id']}")
    repository = elements["repository"]
    if (repository.get("url"), repository.get("commit")) != (item["sourceRepository"], item["sourceCommit"]):
        raise ValueError(f"Package source revision mismatch: {item['id']}")
    return content


def verify_cached_package(item, packages):
    """The restored copy must be the locked archive, fetched from nuget.org."""
    folder = packages / item["id"].lower() / item["version"].lower()
    content = verify_archive(item, folder / f"{item['id']}.{item['version']}.nupkg".lower())
    if hashlib.sha256(content).hexdigest() != item["sha256"]:
        raise ValueError(f"Package SHA-256 mismatch: {item['id']}")
    metadata = json.loads((folder / ".nupkg.metadata").read_text(encoding="utf-8"))
    if metadata["source"] != item["packageSource"]:
        raise ValueError(f"Unexpected resolved package source: {item['id']}: {metadata['source']}")
    archive_hash = base64.b64encode(hashlib.sha512(content).digest()).decode()
    if (folder / f"{item['id']}.{item['version']}.nupkg.sha512".lower()).read_text().strip() != archive_hash:
        raise ValueError(f"Cached archive SHA-512 mismatch: {item['id']}")
    # The content hash excludes the repository signature; the archive hash includes it.
    if metadata["contentHash"] != item["contentHash"]:
        raise ValueError(f"Resolved package content mismatch: {item['id']}")
    return folder


def project_roots():
    return [ROOT / name for name in ("src", "tests", "benchmarks") if (ROOT / name).is_dir()]


def verify(packages=None):
    lock = json.loads((ROOT / "dependency-source.json").read_text(encoding="utf-8"))
    if lock.get("packages"):
        raise ValueError("Private feed packages are not permitted; every dependency must be published")
    verify_mappings()
    published = lock["publishedPackages"]
    declared = {item["id"]: item["version"] for item in published}
    if len(declared) != len(published):
        raise ValueError("Duplicate dependency identities")
    for item in published:
        if item["packageSource"] != NUGET_ORG:
            raise ValueError("Locked dependencies must come from nuget.org")
    central = ET.parse(ROOT / "Directory.Packages.props")
    for item in central.iter("PackageVersion"):
        name = item.attrib["Include"]
        if name.startswith(FAMILY):
            # A pin no project restores is harmless: using it later fails the assets scan below.
            if name in declared and item.attrib["Version"] != "[" + declared[name] + "]":
                raise ValueError(f"Central pin differs from the locked closure: {name}")
    resolved = set()
    if packages is not None:
        for project_root in project_roots():
            for project in project_root.rglob("*.csproj"):
                assets = json.loads((project.parent / "obj/project.assets.json").read_text(encoding="utf-8"))
                for identity, library in assets["libraries"].items():
                    if library.get("type") == "project":
                        continue
                    name, version = identity.rsplit("/", 1)
                    if name.startswith(FAMILY) and name not in declared:
                        raise ValueError(f"Unlocked ecosystem dependency: {identity}")
                    if name in declared:
                        if version != declared[name]:
                            raise ValueError(f"Unexpected resolved package version: {identity}")
                        resolved.add(name)
        if not resolved:
            raise ValueError("No locked dependencies found in solution restore assets")
        for item in published:
            if item["id"] in resolved:
                verify_cached_package(item, packages)
    print(f"PASS: {len(declared)} locked package identities; central pins match; source nuget.org")
    if packages is not None:
        print(f"PASS: all {len(resolved)} referenced dependencies resolved from signed nuget.org archives")
        unused = sorted(set(declared) - resolved)
        if unused:
            print("Locked packages not referenced by this solution: " + ", ".join(unused))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path)
    args = parser.parse_args()
    try:
        verify(args.packages)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
        parser.exit(1, f"FAIL: {error}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
