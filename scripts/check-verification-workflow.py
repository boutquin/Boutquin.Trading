#!/usr/bin/env python3
"""Validate the parsed CI verification commands against the repository solution."""

import argparse
import copy
import json
from pathlib import Path
import re
import shlex
import unittest

import yaml

SOLUTION = "Boutquin.Trading.slnx"
VERIFICATION_COMMANDS = {"restore", "build", "test", "pack", "format"}
DOTNET_VERIFICATION = re.compile(
    r"(?:^|[\s;(])dotnet\s+(?:restore|build|test|pack|format)\b",
    re.IGNORECASE,
)


def violations(document):
    errors = []
    commands = []
    for job in document.get("jobs", {}).values():
        for step in job.get("steps", []):
            for line in step.get("run", "").replace("\\\n", " ").splitlines():
                stripped = line.strip()
                if not stripped or stripped.startswith("#") or not DOTNET_VERIFICATION.search(stripped):
                    continue
                if not stripped.startswith("dotnet "):
                    errors.append(f"Unsupported wrapped dotnet verification command: {stripped}")
                    continue
                try:
                    words = shlex.split(stripped, comments=True)
                except ValueError as error:
                    errors.append(f"Malformed dotnet verification command: {stripped} ({error})")
                    continue
                if len(words) < 2 or words[1] not in VERIFICATION_COMMANDS:
                    continue
                commands.append(words)
                if SOLUTION not in words:
                    errors.append(f"Missing explicit solution: {stripped}")
    if not commands:
        errors.append("No dotnet verification commands found")
    return errors


def dependency_violations(document, lock):
    """Require the private feed before every cold solution restore."""
    errors = []
    for job in document.get("jobs", {}).values():
        steps = job.get("steps", [])
        for index, step in enumerate(steps):
            run = step.get("run", "")
            if not re.search(r"\bdotnet\s+restore\b", run):
                continue
            if "--configfile nuget.ci.config" not in run or "--no-http-cache" not in run:
                errors.append("Restore must use nuget.ci.config and disable the HTTP cache")
            earlier = steps[:index]
            checkouts = [s.get("with", {}) for s in earlier if s.get("uses", "").startswith("actions/checkout@")]
            if lock.get("packages") and not any(
                checkout.get("repository") == lock["repository"]
                and checkout.get("ref") == lock["revision"]
                and checkout.get("path") == "artifacts/dependency-source"
                and checkout.get("ssh-key") == "${{ secrets.DEPENDENCY_SOURCE_SSH_KEY }}"
                and checkout.get("persist-credentials") is False
                for checkout in checkouts
            ):
                errors.append("Restore requires the pinned dependency checkout with read-only key access")
            if not any(s.get("run") == "python3 scripts/check-runner-dependencies.py" for s in earlier):
                errors.append("Dependency bytes must be verified before restore")
            if not any(s.get("run") == 'python3 scripts/check-runner-dependencies.py --packages "$NUGET_PACKAGES"' for s in steps[index+1:]):
                errors.append("Resolved dependency provenance must be verified after restore")
            cold = [s.get("run", "") for s in earlier if s.get("name") == "Initialize cold NuGet directories"]
            required = (
                "$RUNNER_TEMP", "$GITHUB_RUN_ID", "$GITHUB_RUN_ATTEMPT", "$GITHUB_ENV",
                "NUGET_PACKAGES=", "NUGET_HTTP_CACHE_PATH=",
                'mkdir "$DEPENDENCY_PACKAGES_PATH" "$DEPENDENCY_HTTP_CACHE_PATH"',
            )
            if len(cold) != 1 or not all(marker in cold[0] for marker in required):
                errors.append("NuGet directories must be created empty for this runner attempt before restore")
            if any("runner." in str(value) for value in job.get("env", {}).values()):
                errors.append("Runner context is unavailable in job-level env")
            if any(s.get("uses", "").startswith("actions/cache@") for s in steps):
                errors.append("Cold dependency verification must not restore a package cache")
    return errors


def gate_violations(document, filename):
    errors = []
    jobs = document.get("jobs", {})
    for job in jobs.values():
        if job.get("if") is False or str(job.get("if", "")).strip() in ("false", "${{ false }}"):
            errors.append("Verification job is disabled")
        steps = job.get("steps", [])
        tests = [i for i, step in enumerate(steps) if "dotnet test " + SOLUTION in step.get("run", "")]
        if not tests:
            errors.append("Full solution tests must execute")
        for index, step in enumerate(steps):
            if "dotnet nuget push" in step.get("run", ""):
                if step.get("if") != "github.event_name == 'push' && github.repository == 'boutquin/Boutquin.Trading'":
                    errors.append("Publication requires public-repository tag-push guard")
                if not tests or tests[0] >= index:
                    errors.append("Tests must pass before publication")
    if filename == "pr-verify.yml":
        platforms = jobs.get("build", {}).get("strategy", {}).get("matrix", {}).get("os", [])
        if set(platforms) != {"ubuntu-latest", "macos-latest", "windows-latest"}:
            errors.append("All three platforms must execute")
    return errors


class WorkflowContractTests(unittest.TestCase):
    def test_platform_and_publication_gates_reject_disabled_or_missing_tests(self):
        root = Path(__file__).resolve().parents[1]
        for filename in ("pr-verify.yml",):
            document = yaml.safe_load((root / ".github/workflows" / filename).read_text())
            self.assertEqual([], gate_violations(document, filename))
            for mutation in ("disabled", "no-tests", "weakened"):
                changed = copy.deepcopy(document)
                job = next(iter(changed["jobs"].values()))
                if mutation == "disabled":
                    job["if"] = False
                elif mutation == "no-tests":
                    job["steps"] = [step for step in job["steps"] if "dotnet test" not in step.get("run", "")]
                elif filename == "pr-verify.yml":
                    job["strategy"]["matrix"]["os"].remove("windows-latest")
                else:
                    for step in job["steps"]:
                        if "dotnet nuget push" in step.get("run", ""):
                            step.pop("if")
                self.assertTrue(gate_violations(changed, filename), (filename, mutation))

    def test_real_workflows_reject_missing_moved_or_substituted_dependency_source(self):
        root = Path(__file__).resolve().parents[1]
        lock = json.loads((root / "dependency-source.json").read_text(encoding="utf-8"))
        for filename in ("pr-verify.yml",):
            document = yaml.safe_load((root / ".github/workflows" / filename).read_text(encoding="utf-8"))
            self.assertEqual([], dependency_violations(document, lock))
            for mutation in (("missing", "late", "revision") if lock.get("packages") else ()) + ("warm", "local-config", "skip-bytes", "skip-provenance"):
                with self.subTest(filename=filename, mutation=mutation):
                    changed = copy.deepcopy(document)
                    job = next(iter(changed["jobs"].values()))
                    steps = job["steps"]
                    feed = next((s for s in steps if s.get("with", {}).get("repository") == lock.get("repository", "unused")), None)
                    if mutation == "missing":
                        steps.remove(feed)
                    elif mutation == "late":
                        steps.remove(feed)
                        steps.append(feed)
                    elif mutation == "revision":
                        feed["with"]["ref"] = "main"
                    elif mutation == "warm":
                        cold = next(s for s in steps if s.get("name") == "Initialize cold NuGet directories")
                        cold["run"] = cold["run"].replace("$RUNNER_TEMP", "$HOME")
                    elif mutation == "local-config":
                        restore = next(s for s in steps if "dotnet restore" in s.get("run", ""))
                        restore["run"] = restore["run"].replace("nuget.ci.config", "nuget.config")
                    else:
                        command = "python3 scripts/check-runner-dependencies.py"
                        if mutation == "skip-provenance":
                            command += ' --packages "$NUGET_PACKAGES"'
                        steps.remove(next(s for s in steps if s.get("run") == command))
                    self.assertTrue(dependency_violations(changed, lock))

    def test_job_env_rejects_runner_context_before_scheduling(self):
        root = Path(__file__).resolve().parents[1]
        lock = json.loads((root / "dependency-source.json").read_text(encoding="utf-8"))
        for filename in ("pr-verify.yml",):
            document = yaml.safe_load((root / ".github/workflows" / filename).read_text(encoding="utf-8"))
            job = next(iter(document["jobs"].values()))
            job["env"] = {"NUGET_PACKAGES": "${{ runner.temp }}/packages"}
            self.assertIn("Runner context is unavailable in job-level env", dependency_violations(document, lock))

    def test_real_yaml_requires_explicit_solution_for_each_command(self):
        for command in ("restore", "build", "test", "pack", "format whitespace", "format style", "format analyzers"):
            with self.subTest(command=command):
                good = yaml.safe_load(f"jobs:\n  build:\n    steps:\n      - run: dotnet {command} {SOLUTION}\n")
                bad = yaml.safe_load(f"jobs:\n  build:\n    steps:\n      - run: dotnet {command}\n")
                self.assertEqual([], violations(good))
                self.assertTrue(violations(bad))

    def test_empty_workflow_is_not_a_pass(self):
        self.assertTrue(violations({}))

    def test_wrapped_verification_commands_are_rejected_after_a_valid_direct_command(self):
        for wrapped in ("env CHECK=1 dotnet test", "(dotnet test)"):
            with self.subTest(wrapped=wrapped):
                document = yaml.safe_load(
                    "jobs:\n  build:\n    steps:\n"
                    f"      - run: |\n          dotnet build {SOLUTION}\n          {wrapped}\n"
                )
                self.assertEqual(
                    [f"Unsupported wrapped dotnet verification command: {wrapped}"],
                    violations(document),
                )

    def test_unrelated_multiline_shell_is_not_tokenized_as_a_dotnet_command(self):
        document = yaml.safe_load(
            "jobs:\n  publish:\n    steps:\n      - run: |\n"
            "          awk '\n"
            "            /^## \\[/{ flag=1 }\n"
            "          ' CHANGELOG.md\n"
            f"          dotnet pack {SOLUTION} --configuration Release\n"
        )

        self.assertEqual([], violations(document))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workflow", nargs="?", type=Path, default=Path(".github/workflows/pr-verify.yml"))
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        result = unittest.TextTestRunner().run(unittest.defaultTestLoader.loadTestsFromTestCase(WorkflowContractTests))
        return 0 if result.wasSuccessful() else 1
    errors = violations(yaml.safe_load(args.workflow.read_text(encoding="utf-8")))
    root = Path(__file__).resolve().parents[1]
    lock = json.loads((root / "dependency-source.json").read_text(encoding="utf-8"))
    errors += dependency_violations(yaml.safe_load(args.workflow.read_text(encoding="utf-8")), lock)
    errors += gate_violations(yaml.safe_load(args.workflow.read_text(encoding="utf-8")), args.workflow.name)
    for error in errors:
        print(error)
    if not errors:
        print(f"PASS: all dotnet verification commands name {SOLUTION}")
    return bool(errors)


if __name__ == "__main__":
    raise SystemExit(main())
