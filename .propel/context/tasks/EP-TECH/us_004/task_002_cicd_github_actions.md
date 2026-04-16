# Task - task_002_cicd_github_actions

## Requirement Reference
- User Story: [us_004] (.propel/context/tasks/EP-TECH/us_004/us_004.md)
- Story Location: `.propel/context/tasks/EP-TECH/us_004/us_004.md`
- Acceptance Criteria:
  - AC-1: PR opened → GitHub Actions triggers → pipeline executes build, test, and lint stages → reports pass/fail status on the PR.
  - AC-3: Merge to main → pipeline generates an IIS-deployable web deploy package artifact.
  - AC-4: No paid third-party services or proprietary tools used; all CI/CD dependencies are free and open-source per FR-020 and NFR-015.
- Edge Case:
  - Flaky tests: Pipeline retries failed `dotnet test` step once (retries: 2) before marking the job as failed; test output artifacts (`.trx` files) are uploaded with `if: always()` so they are preserved even when tests fail.

## Design References (Frontend Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack
| Layer | Technology | Version |
|-------|------------|---------|
| CI/CD Platform | GitHub Actions | - |
| Runner | ubuntu-latest (GitHub-hosted) | - |
| .NET SDK Action | actions/setup-dotnet | v4 |
| Node Action | actions/setup-node | v4 |
| Checkout Action | actions/checkout | v5 |
| Artifact Action | actions/upload-artifact | v4 |
| NuGet Cache | actions/cache | v4 |
| Test Retry | nick-fields/retry | v3 (OSS) |
| AI/ML | N/A | - |
| Mobile | N/A | - |

**Note:** Only free GitHub-hosted runners and OSS actions used. No paid CI services per AC-4 / FR-020 / NFR-015.

## AI References (AI Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Creates two GitHub Actions workflow files: `ci.yml` (triggered on every PR targeting `main`) and `cd.yml` (triggered on every push to `main`). The CI workflow runs a `dotnet-ci` job (restore → build → format check → test with retry → upload `.trx` artifacts) and a `frontend-ci` job (npm ci → lint → build) in parallel; both jobs must pass for the PR status check to be green. The CD workflow runs after CI on `main` merges: it restores, publishes the ASP.NET Core API (`-c Release`), zips the publish output into a versioned `PropelIQ-<run_number>.zip`, and uploads it as a GitHub Actions artifact ready for manual IIS web deploy — with no paid cloud deployment service, satisfying TR-016 and FR-020.

## Dependent Tasks
- us_004/task_001_infra_docker_fullstack — Docker Compose stack must be committed to the repository for the CI environment to be well-defined.
- us_002/task_001_be_solution_modular_structure — `PropelIQ.sln` and project files must exist for `dotnet restore` and `dotnet build` to succeed in CI.
- us_001/task_001_fe_project_scaffolding — `client/package.json` and `npm run lint`/`npm run build` scripts must exist for frontend CI job.

## Impacted Components
- `.github/workflows/ci.yml` — NEW: PR trigger CI workflow (dotnet-ci + frontend-ci parallel jobs)
- `.github/workflows/cd.yml` — NEW: main push CD workflow (publish + zip + upload artifact)
- `.github/workflows/README.md` — NEW: OSS tooling audit record (FR-020 compliance evidence)

## Implementation Plan

1. **Create `.github/workflows/ci.yml` — PR trigger and dotnet-ci job** — Define trigger:
   ```yaml
   on:
     pull_request:
       branches: [main]
   ```
   Define `dotnet-ci` job on `ubuntu-latest`:
   - `actions/checkout@v5`
   - `actions/setup-dotnet@v4` with `dotnet-version: '8.0.x'`
   - NuGet package cache via `actions/cache@v4` keyed on `**/packages.lock.json` hash (speeds up subsequent runs per NFR-002 performance analogy)
   - `dotnet restore server/PropelIQ.sln`
   - `dotnet build server/PropelIQ.sln --configuration Release --no-restore`
   - `dotnet format server/PropelIQ.sln --verify-no-changes` — fails CI with diff output if formatting violations exist

2. **Add test step with retry** — Inside `dotnet-ci`, use `nick-fields/retry@v3` (OSS, Apache 2.0) to wrap `dotnet test`:
   ```yaml
   - uses: nick-fields/retry@v3
     with:
       timeout_minutes: 10
       max_attempts: 2
       command: >
         dotnet test server/PropelIQ.sln
           --no-build
           --configuration Release
           --logger trx
           --results-directory TestResults
   ```
   `max_attempts: 2` means one initial attempt + one retry before failing — satisfies the flaky-test edge case.

3. **Upload test result artifacts** — After the retry step, always upload `.trx` files:
   ```yaml
   - name: Upload test results
     uses: actions/upload-artifact@v4
     if: always()
     with:
       name: test-results-${{ github.run_number }}
       path: TestResults/
       retention-days: 14
   ```
   `if: always()` ensures artifacts are preserved even when tests fail, enabling post-mortem analysis.

4. **Add frontend-ci job** — Define a second parallel job `frontend-ci` on `ubuntu-latest`:
   - `actions/checkout@v5`
   - `actions/setup-node@v4` with `node-version: '20'`
   - `actions/cache@v4` for `client/node_modules` keyed on `client/package-lock.json`
   - `npm ci --prefix client`
   - `npm run lint --prefix client` (eslint with `--max-warnings 0` — any warning fails CI)
   - `npm run build --prefix client` (Vite production build — validates no TypeScript or import errors)

5. **Add PR status check requirement comment** — Add a top-level `concurrency` group to cancel in-progress CI runs for the same PR on new commits:
   ```yaml
   concurrency:
     group: ci-${{ github.ref }}
     cancel-in-progress: true
   ```
   This prevents redundant runs consuming free runner minutes per NFR-015.

6. **Create `.github/workflows/cd.yml` — main branch push and publish** — Define trigger:
   ```yaml
   on:
     push:
       branches: [main]
   ```
   Define `build-and-package` job on `ubuntu-latest`:
   - `actions/checkout@v5`
   - `actions/setup-dotnet@v4` with `dotnet-version: '8.0.x'`
   - `dotnet restore server/PropelIQ.sln`
   - `dotnet publish server/src/PropelIQ.Api/PropelIQ.Api.csproj -c Release -o ./publish/propeliq --no-restore`

7. **Package IIS web deploy artifact** — After publish, create a versioned ZIP:
   ```yaml
   - name: Package IIS web deploy artifact
     run: |
       cd publish
       zip -r ../PropelIQ-${{ github.run_number }}.zip propeliq/
   - name: Upload IIS web deploy package
     uses: actions/upload-artifact@v4
     with:
       name: iis-web-deploy-pkg-${{ github.run_number }}
       path: PropelIQ-${{ github.run_number }}.zip
       retention-days: 30
   ```
   The ZIP contains the full `dotnet publish` output — assemblies, `web.config`, `appsettings.json`, publish profile — ready for IIS Web Deploy or `robocopy` deployment per us_002/task_002_be_swagger_healthcheck_iis.

8. **Create `.github/workflows/README.md` — OSS audit record** — Document the tooling inventory listing every action used and its license, confirming no paid CI services — providing the verifiable evidence required by AC-4 / FR-020:

   | Action / Tool | Version | License | Cost |
   |---------------|---------|---------|------|
   | actions/checkout | v5 | MIT | Free |
   | actions/setup-dotnet | v4 | MIT | Free |
   | actions/setup-node | v4 | MIT | Free |
   | actions/cache | v4 | MIT | Free |
   | actions/upload-artifact | v4 | MIT | Free |
   | nick-fields/retry | v3 | MIT | Free |
   | ubuntu-latest runner | - | - | Free (public repo) |

## Current Project State

```
.github/
  (workflows/ folder does not exist yet)
server/
  docker-compose.yml        ← Full stack (from us_004/task_001)
  Dockerfile                ← Multi-stage .NET 8 (from us_004/task_001)
  PropelIQ.sln              ← Solution with all 13 projects
  src/PropelIQ.Api/
client/
  package.json              ← npm run lint + npm run build scripts present
  Dockerfile.dev            ← Vite dev server (from us_004/task_001)
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `.github/workflows/ci.yml` | PR trigger: `dotnet-ci` (restore → build → format → test/retry → upload artifacts) + `frontend-ci` (npm ci → lint → build) parallel jobs; concurrency cancel-in-progress |
| CREATE | `.github/workflows/cd.yml` | Push to main trigger: dotnet publish Release → zip → upload `iis-web-deploy-pkg-<run>` artifact |
| CREATE | `.github/workflows/README.md` | OSS tooling audit table documenting all action licenses; FR-020 compliance evidence |

## External References
- GitHub Actions workflow syntax — triggers and jobs: https://docs.github.com/en/actions/writing-workflows/workflow-syntax-for-github-actions
- `actions/setup-dotnet@v4`: https://github.com/actions/setup-dotnet
- `actions/upload-artifact@v4` with `if: always()`: https://docs.github.com/en/actions/tutorials/store-and-share-data
- `nick-fields/retry@v3` (OSS test retry action): https://github.com/nick-fields/retry
- `dotnet publish` for Release packaging: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
- `dotnet format --verify-no-changes` (code style gate): https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
- GitHub Actions NuGet caching: https://docs.github.com/en/actions/how-tos/writing-workflows/caching-dependencies-to-speed-up-workflows
- TR-016 (GitHub Actions CI/CD + IIS deployment), TR-024 (Docker dev, IIS prod), FR-020 (OSS only), NFR-015 (free tools), DR-014 (zero-downtime deployment readiness)

## Build Commands
```bash
# No local build commands — GitHub Actions runs on PR/push events.
# To validate CI YAML syntax locally:
gh workflow list
gh run list --workflow=ci.yml

# To manually trigger CD workflow (after merging to main):
git push origin main
# Monitor at: https://github.com/<org>/propeliq/actions

# To verify dotnet publish output locally (mirrors CD step):
cd server
dotnet publish src/PropelIQ.Api/PropelIQ.Api.csproj -c Release -o ./publish/propeliq
ls ./publish/propeliq/   # Should contain PropelIQ.Api.dll, web.config, appsettings*.json

# To verify frontend lint+build locally (mirrors frontend-ci job):
cd client
npm ci
npm run lint
npm run build
```

## Implementation Validation Strategy
- [ ] Unit tests pass (xUnit suite referenced by ci.yml passes locally before pushing)
- [ ] `.github/workflows/ci.yml` YAML is valid — no syntax errors (`gh workflow view ci.yml` or online YAML linter)
- [ ] Opening a PR against `main` triggers the CI workflow; both `dotnet-ci` and `frontend-ci` jobs appear in the GitHub Actions tab
- [ ] A PR with a failing test shows `failed` check status on the PR; re-run confirms retry fires once before marking failed
- [ ] A PR with a lint error (intentional) fails the `frontend-ci` job and blocks the PR merge
- [ ] Merging to `main` triggers `cd.yml`; artifact `iis-web-deploy-pkg-<N>` appears in the workflow run artifacts
- [ ] Downloaded ZIP artifact contains `PropelIQ.Api.dll`, `web.config`, and `appsettings.json`
- [ ] `.github/workflows/README.md` lists all actions with license and confirms no paid services (FR-020 audit evidence)
- [ ] No secrets, tokens, or credentials committed to workflow YAML files — sensitive values use `${{ secrets.* }}` only (OWASP A02)

## Implementation Checklist
- [x] Create `.github/workflows/ci.yml`: trigger on `pull_request` to `main`; `dotnet-ci` job with `setup-dotnet@v4`, NuGet cache, `dotnet restore`, `dotnet build --configuration Release`, `dotnet format --verify-no-changes`
- [x] Add test retry step in `dotnet-ci` using `nick-fields/retry@v3` (`max_attempts: 2`, `dotnet test --logger trx --results-directory TestResults`); add `upload-artifact@v4` with `if: always()` for TestResults/ folder
- [x] Add `frontend-ci` parallel job in `ci.yml`: `setup-node@v4` (Node 20), node_modules cache, `npm ci`, `npm run lint`, `npm run build`; add `concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: true }`
- [x] Create `.github/workflows/cd.yml`: trigger on `push` to `main`; `build-and-package` job with `setup-dotnet@v4`, `dotnet restore`, `dotnet publish -c Release -o ./publish/propeliq`
- [x] Add packaging step in `cd.yml`: `zip -r PropelIQ-${{ github.run_number }}.zip publish/propeliq/`; upload with `upload-artifact@v4` as `iis-web-deploy-pkg-${{ github.run_number }}`, `retention-days: 30`
- [x] Create `.github/workflows/README.md` with OSS tooling audit table (actions, versions, licenses, cost) as FR-020 compliance evidence
- [x] Verify no paid services, third-party SaaS tokens, or proprietary actions appear in either workflow file
- [ ] Push branch, open PR against `main`, confirm both CI jobs trigger and report status checks on the PR
