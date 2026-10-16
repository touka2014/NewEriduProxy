# Changelog

All notable changes to New Eridu Proxy are documented in this file.

The project follows [Semantic Versioning](https://semver.org/). Feature milestones may also receive a codename; prerelease and patch builds inherit the name of their parent milestone.

## 0.1.0-beta.2 - Unreleased — Signal Search

### Security

- Added timeout protection to subscription, policy-group, and log regex filters.
- Blocked update archive entries that resolve outside the application directory.

### Added

- Upstream MASQUE support and Xray TUN enhancements.
- Real-core smoke checks for four simultaneous listeners, LAN policies, port conflicts, live traffic, daily statistics, and cleanup.

### Changed

- Integrated v2rayN 7.25.6 and subsequent mainline fixes through `95f438a`.
- Pinned Xray `26.10.10` and sing-box `1.14.3` with verified archive hashes.
- Inherited upstream download, latency-test, UDP-test, DNS, and interface improvements.
- Preserved the legacy prerelease setting while migrating to per-core update selections.
- Made release automation validate tag/version alignment and run real-core checks before packaging.

### Fixed

- Isolated sing-box caches so multiple nodes can start without sharing a database lock.
- Prevented legacy global LAN-port settings from creating extra listeners for parallel nodes.
- Preserved parallel core startup output for troubleshooting.
- Replaced remaining client labels in settings, update rows, notifications, and startup logs with New Eridu Proxy while preserving stored update identifiers.

### Validation

- 151 automated tests passed.
- Two Xray and two sing-box processes passed local transfer and lifecycle checks.
- Manual application validation is required before publishing this prerelease.

## [0.1.0-beta.1] - 2026-09-11 — Signal Search

### Added

- Parallel startup for multiple Xray, v2fly, v2fly v5, and sing-box nodes.
- A dedicated mixed inbound port and LAN access policy for each node.
- Conflict-checked port allocation from the `40000-48999` range.
- Consecutive port organization for selected nodes.
- Per-node live upload, live download, daily traffic, and lifetime traffic statistics.
- A dedicated switch for the traditional main local inbound on port `20808`.

### Changed

- Reworked the server list around parallel listener management.
- Fixed the interface language to English.
- Rebranded the Windows application as New Eridu Proxy.
- Redirected application update checks to the New Eridu Proxy release feed.
- Updated the bundled upgrade helper for the new executable and process name.

### Removed

- The Promotion menu and its external advertising content.

### Notes

- The main local inbound is disabled by default.
- Per-node LAN access is disabled by default and listens on `127.0.0.1` until enabled.
- This is a prerelease intended for wider testing before `v0.1.0`.

[0.1.0-beta.1]: https://github.com/touka2014/NewEriduProxy/releases/tag/v0.1.0-beta.1
