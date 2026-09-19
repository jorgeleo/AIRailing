#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: GITHUB_TOKEN=... $0 <package-version>" >&2
  exit 2
fi

if [ -z "${GITHUB_TOKEN:-}" ]; then
  echo "GITHUB_TOKEN must be a GitHub token with packages:write permission." >&2
  exit 2
fi

publish_version="$1"
package_output="artifacts/packages"
package_source="https://nuget.pkg.github.com/jorgeleo/index.json"

dotnet test Hawthorne.sln --configuration Release
dotnet pack src/Hawthorne.Analyzers/Hawthorne.Analyzers.csproj --configuration Release --no-build --output "$package_output" -p:Version="$publish_version"
dotnet nuget push "$package_output/Hawthorne.Analyzers.$publish_version.nupkg" --source "$package_source" --api-key "$GITHUB_TOKEN" --skip-duplicate
