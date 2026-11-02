#!/usr/bin/env python3
"""Fail qualification when NuGet's JSON audit reports errors or vulnerabilities."""
import json
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def violations(report):
    errors = [entry for entry in report.get("logs", []) if entry.get("level", "").lower() == "error"]
    for project in report.get("projects", []):
        for framework in project.get("frameworks", []):
            for kind in ("topLevelPackages", "transitivePackages"):
                errors.extend(package for package in framework.get(kind, []) if package.get("vulnerabilities"))
    return errors


if __name__ == "__main__":
    report = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
    parameters = report.get("parameters", "").split()
    if report.get("version") != 1 or not {"--vulnerable", "--include-transitive"}.issubset(parameters):
        raise SystemExit("FAIL: expected version-1 transitive vulnerability audit")
    expected = {str((ROOT / project.get("Path")).resolve()) for project in ET.parse(ROOT / "Boutquin.Trading.slnx").iter("Project")}
    actual = [str(Path(project.get("path", "")).resolve()) for project in report.get("projects", [])]
    if set(actual) != expected or len(actual) != len(expected):
        raise SystemExit("FAIL: dependency audit did not enumerate exactly the full solution")
    # NuGet omits frameworks entirely when a project has no vulnerable packages.

    found = violations(report)
    if found:
        print(json.dumps(found, indent=2))
        raise SystemExit(1)
    print("PASS: NuGet audit returned no errors or vulnerable packages")
