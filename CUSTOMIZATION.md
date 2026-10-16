# New Eridu Proxy Customization Notes

This branch extends v2rayN with parallel multi-node proxy operation and removes the Promotion entry. The development build is `v0.1.0-beta.2`, codenamed **Signal Search**, based on upstream v2rayN 7.25.6 with mainline changes through `95f438a`. It awaits manual validation on `upgrade/v2rayn-7.25.6`. The published beta.1 remains available, along with the historical integration tags.

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

The preparation script pins Xray `26.10.10` and sing-box `1.14.3` and verifies both archives against recorded SHA-256 values. Xray `26.10.10` is an official prerelease and matches the version referenced by upstream v2rayN 7.25.6.
