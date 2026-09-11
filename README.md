# New Eridu Proxy

> Multiple routes. One clean control room.

New Eridu Proxy is an unofficial, English-only Windows fork of [v2rayN](https://github.com/2dust/v2rayN) for users who need several proxy nodes online at the same time. Each node can run through its own mixed inbound port, keep an independent LAN access policy, and report live transfer rates plus daily traffic totals.

This is more than a visual remix: the runtime, configuration flow, statistics pipeline, and server list have been extended for practical multi-node operation while keeping the familiar v2rayN foundation.

## Highlights

- Run multiple selected nodes as independent core processes.
- Assign a dedicated mixed port to every node.
- Allocate new ports from the checked `40000-48999` range.
- Reorganize selected nodes into a conflict-checked consecutive port block.
- Enable LAN access per node instead of applying one global rule.
- View node status, live upload, live download, daily traffic, and total traffic in the server list.
- Use the traditional main local inbound on port `20808` when needed.
- Keep the main local inbound disabled by default with a dedicated switch beside TUN.
- Use an English-only interface for predictable font and layout behavior.
- Remove the Promotion menu and its external advertising link.

## Supported parallel cores

Parallel mixed inbounds are supported for standard nodes using:

- Xray
- v2fly
- v2fly v5
- sing-box

Full custom configurations are intentionally excluded from parallel startup because their inbound and statistics endpoints cannot be rewritten safely without making assumptions about the user's configuration.

## Quick start

1. Import or create your proxy nodes.
2. Select one or more rows with `Ctrl` or `Shift`.
3. Review the inline `LAN access` and `Mixed port` values.
4. Choose **Start selected**.
5. Use **Stop selected** or **Stop all** when those independent listeners are no longer needed.

Choose **Organize selected** to give the selected nodes a checked block of consecutive mixed ports. Running selected nodes are stopped before their ports are reorganized.

The first five server-list columns are always:

1. `LAN access`
2. `Status`
3. `Mixed port`
4. `Live upload`
5. `Live download`

## Listener behavior

| Listener | Default | Bind address | Purpose |
| --- | --- | --- | --- |
| Main local mixed inbound | Port `20808`, disabled | Existing global inbound setting | System proxy, TUN, or traditional single-node workflows |
| Parallel node inbound | Automatically assigned | `127.0.0.1` | Independent local access to one selected node |
| Parallel node with LAN access | Opt-in per node | `0.0.0.0` | Access from trusted devices on the local network |

Before starting or assigning a listener, the application checks active TCP and UDP listeners, the main inbound port, and ports already assigned to other nodes. A conflict stops the operation instead of silently moving the listener.

> [!CAUTION]
> Enabling `LAN access` exposes that node's mixed listener to reachable network interfaces. Only enable it on a trusted network and protect the host with appropriate firewall rules.

## Traffic statistics

Each running parallel node reports live upload and download rates. Daily and lifetime totals are stored locally with the node profile. Daily counters reset when the local calendar date changes, and statistics are saved periodically as well as during a clean application shutdown.

Temporary parallel core configurations are removed when a node stops, when its core exits, and during cleanup after an interrupted previous session.

## Build from source

### Requirements

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PowerShell
- Git with submodule support

Clone the repository and switch to the customized branch:

```powershell
git clone --recurse-submodules https://github.com/touka2014/NewEriduProxy.git
cd NewEriduProxy
git switch new-eridu-proxy
```

Download the pinned Xray and sing-box runtime assets. The script verifies both archives with SHA-256 before copying any files into the build tree:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare-cores.ps1
```

Restore dependencies and build the Windows release:

```powershell
dotnet restore v2rayN\v2rayN.sln
dotnet build v2rayN\v2rayN.sln -c Release
```

The executable is produced at:

```text
v2rayN\v2rayN\bin\Release\net10.0-windows10.0.19041.0\NewEriduProxy.exe
```

Run the test suite with:

```powershell
v2rayN\ServiceLib.Tests\bin\Release\net10.0\ServiceLib.Tests.exe --no-ansi --progress off --output Normal
```

## Releases

The current stable source tag is [`new-eridu-v7.25.1-1`](https://github.com/touka2014/NewEriduProxy/tree/new-eridu-v7.25.1-1), based on v2rayN 7.25.1.

Prebuilt packages are not published yet. A packaged GitHub Release will be added after release presentation and screenshots are finalized. Until then, build the customized branch from source using the verified steps above.

## Branches and upstream updates

- `master` mirrors the official v2rayN upstream branch and does not contain New Eridu customization.
- `new-eridu-proxy` is the stable customized branch.
- `feature/*`, `fix/*`, and `upgrade/*` branches are used for isolated development and upstream integration.

Official updates are merged into a temporary `upgrade/*` branch first. The customized branch is updated only after conflicts are reviewed, automated tests pass, and the Windows release completes a runtime smoke test.

Technical implementation notes are available in [CUSTOMIZATION.md](CUSTOMIZATION.md).

## Credits

New Eridu Proxy is built on the work of the [v2rayN maintainers and contributors](https://github.com/2dust/v2rayN). Core runtime credit belongs to the respective [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box), and v2fly projects.

This community fork is not affiliated with or endorsed by the v2rayN maintainers, HoYoverse, or COGNOSPHERE. Its game-inspired identity is used for this independent customization project.

## License

This repository remains licensed under the [GNU General Public License v3.0](LICENSE), following the upstream v2rayN project.
