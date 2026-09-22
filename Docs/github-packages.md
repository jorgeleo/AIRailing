# Publishing Hawthorne.Analyzers to GitHub Packages

The package ID is `Hawthorne.Analyzers`. GitHub Packages publishes it to the
`jorgeleo` NuGet feed:

```text
https://nuget.pkg.github.com/jorgeleo/index.json
```

Push a version tag such as `v0.1.0`, or run the **Publish Hawthorne.Analyzers**
workflow manually with an explicit version. The workflow tests, packs, and
pushes using GitHub Actions' `GITHUB_TOKEN` with `packages: write` permission.

For a full local release, use the ignored `scripts/release-local.sh` helper.
It requires a clean checkout, GitHub CLI (`gh`), and a `GITHUB_TOKEN` with both
`packages:write` and `contents:write` permission. It increments the patch
version, tests and packs it, commits the version change, creates and pushes an
annotated `vX.Y.Z` tag, publishes to GitHub Packages, and attaches the `.nupkg`
to the corresponding GitHub Release. The release asset is downloadable from
the repository's **Releases** page.

```bash
export GITHUB_TOKEN=...
bash scripts/release-local.sh
```

`gh` automatically uses `GITHUB_TOKEN`; authenticate it separately only when
you choose not to provide the environment variable.

Set `GITHUB_REPOSITORY=owner/repository` before running the helper only when
the release target differs from `jorgeleo/AIRailing`.

To publish only to the NuGet feed without creating a tag, commit, or GitHub
Release, provide a token with `packages:write` permission without placing it in
a file:

```bash
GITHUB_TOKEN=... bash scripts/publish-github-packages.sh 0.1.0
```

Consumers add the feed as a NuGet source and reference
`Hawthorne.Analyzers` at the published version.
