#!/usr/bin/env python3
"""Validate downloaded immutable release assets against an operator-pinned manifest hash."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]

def validate(feed, tag, digest, commit):
    if not re.fullmatch(r"v2\.3\.1(?:-rc\.[0-9]+(?:\.[0-9]+)*)?", tag):
        raise ValueError("Only the explicit 2.3.1 stable or numeric RC tags can be published")
    if not re.fullmatch(r"[0-9a-f]{64}", digest):
        raise ValueError("Expected the independently frozen manifest SHA-256")
    manifest = feed / "package-hashes.json"
    if hashlib.sha256(manifest.read_bytes()).hexdigest() != digest:
        raise ValueError("Release manifest differs from qualified manifest")
    identity = json.loads(manifest.read_text())
    if identity["version"] != tag[1:] or identity["sourceCommit"] != commit:
        raise ValueError("Release tag, manifest and checked-out source differ")
    for filename, expected in identity["sha256"].items():
        if Path(filename).name != filename or not filename.endswith((".nupkg", ".snupkg")):
            raise ValueError("Unsafe release asset path")
        if hashlib.sha256((feed / filename).read_bytes()).hexdigest() != expected:
            raise ValueError("Changed release archive: " + filename)
    return manifest

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("feed", type=Path)
    p.add_argument("tag")
    p.add_argument("manifest_sha256")
    a = p.parse_args()
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    manifest = validate(a.feed, a.tag, a.manifest_sha256, commit)
    subprocess.run([sys.executable, str(ROOT / "scripts/verify-rc-packages.py"), str(a.feed), a.tag[1:], commit,
                    "--manifest", str(manifest), "--report", str(a.feed / "publication-inspection.json")], check=True)
    print("PASS: original qualified archives match the public source tag and pinned manifest")

if __name__ == "__main__":
    main()
