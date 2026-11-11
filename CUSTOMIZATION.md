# New Eridu Proxy Customization Notes

The `new-eridu-proxy` branch extends v2rayN with parallel multi-node proxy operation and removes the Promotion entry. The current prerelease, **Signal Search beta 2**, integrates upstream v2rayN 7.25.6 and subsequent mainline changes through `f07e3dc`. [Release notes for v0.1.0-beta.2](https://github.com/touka2014/NewEriduProxy/releases/tag/v0.1.0-beta.2)

## Parallel node operation

1. Select one or more nodes with `Ctrl` or `Shift`.
2. Set the local listener for each row in **Mixed port**.
3. Choose **Start selected**. Each node starts in an independent core process and listens on `mixed://127.0.0.1:<port>` by default.
4. The server list continuously reports status, live upload, live download, daily traffic, and lifetime traffic.
5. Use **Stop selected** or **Stop all** to terminate the corresponding processes.

The traditional main mixed inbound uses port `20808` and has a dedicated status-bar switch. It is disabled by default, and existing configurations that still use the upstream default of `10808` are migrated automatically. Parallel nodes do not change the system proxy. Xray, v2fly, v2fly v5, and sing-box nodes support parallel operation. Full custom configurations are excluded because their inbound and statistics endpoints cannot be rewritten safely.

The interface is fixed to English. New nodes receive an independent mixed port from the checked `40000-48999` range. Allocation excludes active system TCP and UDP listeners, the main inbound, and ports assigned to other nodes. **Organize selected** assigns a checked consecutive port block to the selected nodes. Each node stores its own **LAN access** setting: disabled listens on `127.0.0.1`, while enabled listens on `0.0.0.0`.

The first five server-list columns are fixed as **LAN access**, **Status**, **Mixed port**, **Live upload**, and **Live download**. Cached column layouts cannot override this order. Temporary core configurations are removed when a parallel process stops or exits, and stale temporary files from an interrupted session are cleaned during startup.

## Data and process lifecycle

- Each node's mixed port is stored in `ProfileExItem`.
- Daily and lifetime traffic remain stored in `ServerStatItem`; daily counters reset when the local calendar date changes.
- Application shutdown stops all parallel core processes and persists traffic counters.
- Startup validates TCP and UDP port availability. A conflict stops the operation instead of silently selecting another port.
- Runtime user data is stored under `guiConfigs` beside the application. Rebuilding the executable does not erase this directory.
- Parallel sing-box instances use independent cache databases, including when the traditional main core is running.
- Parallel listeners ignore the legacy global option that creates an additional LAN port; each node uses its assigned mixed port for its selected bind policy.
- The legacy prerelease update switch is migrated into the new per-core update selections.

## Build verification

Before building the Windows WPF application for the first time, download and verify the pinned Xray, sing-box, and routing assets:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare-cores.ps1
```

The script places these runtime files in the Git-ignored `v2rayN\runtime-assets` directory. Then build the release and run the test suite:

```powershell
dotnet build v2rayN\v2rayN.sln -c Release
v2rayN\ServiceLib.Tests\bin\Release\net10.0\ServiceLib.Tests.exe --no-ansi --progress off --output Normal
dotnet run --project v2rayN\ServiceLib.RuntimeTests\ServiceLib.RuntimeTests.csproj -c Release
```

The Windows WPF executable is named `NewEriduProxy.exe`.

The preparation script pins Xray `26.11.9` and sing-box `1.14.3` and verifies both archives against recorded SHA-256 values. Xray `26.11.9` supports the XDRIVE transport exposed by this mainline snapshot.

## Repository automation

The active workflows follow the New Eridu Proxy Windows x64 release process.

| Workflow | Trigger | Result |
| --- | --- | --- |
| Code Test | Relevant changes on development branches and pull requests, or a manual run | ServiceLib tests and a downloadable TRX report |
| Release New Eridu Proxy | Version tag push or a manual run | Builds and tests Windows x64, exercises parallel cores, and uploads the ZIP and checksum |
| Sync GitHub release notes | Versioned release notes change on `new-eridu-proxy` | Updates the matching published release descriptions |
| Update root certificates | Mondays at 01:23 UTC, or a manual run | Validates Mozilla and Chromium roots, runs ServiceLib tests, and proposes changed bundles in one reusable PR |

Manual Windows builds produce workflow artifacts. Publishing a GitHub Release requires a version tag push. This allows the full packaging workflow to be checked before publishing.

Certificate maintenance requires the repository setting **Allow GitHub Actions to create and approve pull requests**. The default token permission is read-only, with write permissions declared by the workflows that need them. Certificate changes receive validation in the maintenance job because PRs created using `GITHUB_TOKEN` do not trigger the regular pull-request workflow. Maintainers review and merge the proposed updates for a future application build.

The upstream WinGet submission, GPG signing, Web API packaging, and additional platform release workflows have been removed from this branch. Windows downloads are distributed through this repository's GitHub Releases. When integrating upstream changes, review changes under `.github/workflows` against these four workflows. Introducing another distribution channel requires its own New Eridu Proxy package identity and build configuration.
