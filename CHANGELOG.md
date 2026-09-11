# Changelog

All notable changes to New Eridu Proxy are documented in this file.

The project follows [Semantic Versioning](https://semver.org/). Feature milestones may also receive a codename; prerelease and patch builds inherit the name of their parent milestone.

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
