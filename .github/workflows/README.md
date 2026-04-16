# GitHub Actions Workflows — OSS Tooling Audit

FR-020 / AC-4 compliance evidence: all CI/CD dependencies are free and open-source.
No paid third-party services, proprietary actions, or private runners are used.

## Workflows

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| CI | `ci.yml` | PR → `main` | Parallel dotnet-ci + frontend-ci; PR status gate |
| CD | `cd.yml` | Push → `main` | Release publish + IIS web deploy ZIP artifact |

## OSS Action Inventory

| Action | Version | License | Cost | Usage |
|--------|---------|---------|------|-------|
| `actions/checkout` | v5 | MIT | Free | All jobs — source checkout |
| `actions/setup-dotnet` | v4 | MIT | Free | .NET 8 SDK setup |
| `actions/setup-node` | v4 | MIT | Free | Node 20 setup |
| `actions/cache` | v4 | MIT | Free | NuGet + node_modules caching |
| `actions/upload-artifact` | v4 | MIT | Free | Test results + IIS deploy package |
| `nick-fields/retry` | v3 | MIT | Free | Flaky test retry (max 2 attempts) |
| `ubuntu-latest` runner | — | — | Free | GitHub-hosted (public repo) |

## Runner Environment

- All jobs run on `ubuntu-latest` (GitHub-hosted, free for public repositories).
- No self-hosted runners configured.
- No paid CI services (CircleCI, Travis, etc.) referenced.

## Secret Handling

No secrets or credentials are referenced in either workflow file.
Future deployment steps that require credentials (e.g., IIS server connection) must
use `${{ secrets.SECRET_NAME }}` and be registered in GitHub repository Settings → Secrets.
