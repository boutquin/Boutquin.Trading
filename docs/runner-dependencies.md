# Dependency restore and release verification

Every `Boutquin.*` dependency of Trading is a signed package published on nuget.org. `Directory.Packages.props` pins each by exact version, and `dependency-source.json` locks the closure that a restore resolves: archive SHA-256, signature-excluded content hash, original payload hashes, package source and source revision. `nuget.config` and `nuget.ci.config` name only nuget.org with a catch-all source mapping, so a local or private feed cannot supply a same-version package.

Use the SDK pinned by `global.json` (10.0.403, roll-forward disabled). Restore and verify with an empty package cache:

```bash
export NUGET_PACKAGES="$(mktemp -d)"
python3 scripts/check-runner-dependencies.py
dotnet restore Boutquin.Trading.slnx --configfile nuget.ci.config --locked-mode --no-http-cache
python3 scripts/check-runner-dependencies.py --packages "$NUGET_PACKAGES"
dotnet build Boutquin.Trading.slnx --configuration Release --no-restore
python3 scripts/verify-dependency-identities.py --packages "$NUGET_PACKAGES" --outputs . --report dependency-identities.json
dotnet test Boutquin.Trading.slnx --configuration Release --no-build --no-restore
dotnet format Boutquin.Trading.slnx --verify-no-changes --no-restore
```

The provenance step requires each resolved `Boutquin.*` package to come from nuget.org, carry a valid repository signature, match its locked payload and source commit, and match the locked content hash. The identity step checks assembly, file and informational versions of the packaged DLLs and that every built runtime copy is the packaged bytes. After changing a pin, run a cold restore with `--force-evaluate` and `python3 scripts/write-dependency-lock.py --packages "$NUGET_PACKAGES"` to regenerate the lock; the diff must show only the intended packages.

CI repeats these checks on Linux, macOS and Windows, plus format, packaged-XML-documentation, vulnerability and documentation-quality gates.

## Releasing

Release archives are built once from the exact public source commit, qualified, and frozen in a SHA-256 manifest by `scripts/build-rc-candidate.py`. The publication workflow accepts an explicit `v2.3.1` or matching numeric RC tag and the independently recorded manifest hash, downloads that release's original assets, verifies source and package identities, and uploads those same bytes to nuget.org. It never repacks a qualified identity.

The publisher authenticates with trusted publishing: the job's GitHub OIDC token, issued for the `release` environment, is exchanged for a one-hour API key, so no long-lived key is stored. Date the `CHANGELOG.md` section and align the README version and tag links before the source push; `python3 scripts/check-release-docs.py <version>` enforces this, and the publication workflow runs it against the tagged source for stable tags. Run the verification workflow on the public source, create the tag and release assets, then dispatch the publisher.

After publication, `python3 scripts/verify-published-packages.py <version> <candidate packages> <empty work dir>` restores every package from nuget.org into an empty folder, loads each assembly and requires every payload entry to equal the qualified candidate. A populated global NuGet cache can silently serve a different build of the same version, so never verify from one.
