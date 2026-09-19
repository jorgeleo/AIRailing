# Publishing Hawthorne.Analyzers to GitHub Packages

The package ID is `Hawthorne.Analyzers`. GitHub Packages publishes it to the
`jorgeleo` NuGet feed:

```text
https://nuget.pkg.github.com/jorgeleo/index.json
```

Push a version tag such as `v0.1.0`, or run the **Publish Hawthorne.Analyzers**
workflow manually with an explicit version. The workflow tests, packs, and
pushes using GitHub Actions' `GITHUB_TOKEN` with `packages: write` permission.

For a local release, provide a token with `packages:write` permission without
placing it in a file:

```bash
GITHUB_TOKEN=... bash scripts/publish-github-packages.sh 0.1.0
```

Consumers add the feed as a NuGet source and reference
`Hawthorne.Analyzers` at the published version.
