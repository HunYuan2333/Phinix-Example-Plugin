# Phinix Example Plugin

<p align="center">
  English · <a href="./README.zh-CN.md">简体中文</a>
</p>

The official minimal reference implementation for authoring, packaging, and publishing Phinix managed plugins (Package ID: `phinix.example.basic`).

> [!NOTE]
> - **Technical Guide**: For full documentation on public APIs, lifecycle, DI composition, threading, and resource ownership, see the [Phinix Plugin Development Guide](https://github.com/HunYuan2333/Phinix-Rework/blob/dev/docs/Plugin-Development.md).
> - **Implementation Status**: The development source uses `ClientExtensionModule.Compose` and a plugin-owned DI scope. Prepared public host DLLs are borrowed; published 1.0.2 release artifacts are unchanged. See the [local development loop](https://github.com/HunYuan2333/Phinix-Rework/blob/dev/docs/Local-Plugin-Quickstart.md).
> - **Build Status**: Automated GitHub Actions compilation (automatic build/package on main push, no build on dev) is actively scheduled under F7; packaging currently uses the local `pack.py` entry point.
> - **Skill Notice**: The Phinix plugin development skill is currently in preparation; integration links will be added once ready.

---

## Overview & Features

This example demonstrates the standard lifecycle, UI extension patterns, and distribution process for managed plugins:
- **Custom Localized Tab**: Registers a top-level tab in the Phinix window using `IMainTabProvider`.
- **Persistent Settings**: Saves user configuration using `IClientSettingsContext` with package-scoped keys.
- **Interactive State**: Implements an interactive click counter, dynamic layout hints, and reset confirmation dialogs.
- **Host Localization**: Loads package-scoped English (`en-US.json`) and Simplified Chinese (`zh-CN.json`) strings.
- **Safe Execution**: **No silver or item generation, no colony modifications, and no external network calls.**

---

## Player Experience

To try this plugin in RimWorld:
1. Open the in-game Phinix window and switch to the **Store** (`商店`) tab.
2. Locate **Phinix Example Plugin** and click **Install** (`安装`).
3. **Restart RimWorld** to load the plugin assembly.
4. Open the Phinix window:
   - Click the new **Example** (`示例`) tab and increment the click counter.
   - Go to **Settings** (`设置`) → **Example** to toggle explanation tooltips or reset the counter.
   - Switch the game language between English and Simplified Chinese to observe automatic text updates.
5. In **Extension Manager** (`扩展管理`), test disabling or uninstalling the plugin, then restart the game to confirm clean teardown.

---

## Source Code Map

| Responsibility | File / Symbol | Description |
| :--- | :--- | :--- |
| **Module Entry & Lifecycle** | `example/ExampleExtension.cs` (`ExampleExtension`) | Uses `ClientExtensionModule.Compose`, owns a DI scope, and activates/releases state through the ordinary lifecycle. |
| **State & Settings Management** | `example/ExampleExtension.cs` (`ExampleState`) | Stores persisted keys (e.g., `phinix.example.basic.clicks`) and manages localization events. |
| **Tab UI Rendering** | `example/ExampleExtension.cs` (`ExampleTab`) | Implements `IMainTabProvider` to draw responsive buttons and text. |
| **Settings Panel** | `example/ExampleExtension.cs` (`ExampleSettingsPanel`) | Implements `IClientSettingsPanelProvider` to render settings checkboxes and action buttons. |
| **Dual-Language Resources** | `example/Resources/Localization/{en-US,zh-CN}.json` | Package-scoped translations with placeholder format arguments. |
| **Packaging Tool** | `pack.py` | Invokes the official `ManagedPackageTool` to generate schema-compliant release ZIPs. |

---

## Build & Packaging

### Prerequisites

- **.NET 10 SDK** (runs compilation and packaging tools)
- **Prepared Public Host DLLs**: `Utils` and `ClientExtensionAbstractions` from a built/installed Phinix Rework. Plugin builds do not rebuild the host.
- **RimWorld 1.6 References**: Compile-only assemblies (`Assembly-CSharp.dll`, `UnityEngine*.dll`). Never commit or distribute game assemblies.

### Quick Packaging Command

From this repository's root, execute:

```bash
python3 pack.py \
  --phinix-root <path-to-Phinix-Rework> \
  --game-references <path-to-RimWorld-Managed> \
  --output <path-to-output>/phinix-example-basic-1.0.2.zip
```

To create an uncompressed developer folder for direct local testing, add:
`--bundle-output <path-to-output>/phinix-example-basic/`

---

## Package Structure

A valid Phinix managed plugin package maintains this internal directory layout:

```text
phinix-example-basic-1.0.2.zip
├── manifest.json
├── Assemblies/
│   └── Phinix.Example.Basic.dll
└── Resources/
    └── Localization/
        ├── en-US.json
        └── zh-CN.json
```

- **`manifest.json`**: Package identifier, version, declared assemblies, target Phinix version range, and dependencies.
- **`Assemblies/`**: Contains only the plugin's own compiled DLLs.
- **`Resources/`**: Contains assets and localization dictionaries scoped strictly to this package.

> [!CAUTION]
> The ZIP package must **never** contain RimWorld game assemblies (`Assembly-CSharp.dll`, `UnityEngine*.dll`), host assemblies (`Utils.dll`, `ClientExtensionAbstractions.dll`), or Harmony. Including host or game DLLs causes runtime assembly clashes and triggers automated rejection by the index validator.

---

## Localization & Fallbacks

- **Dual-Language Dictionaries**: Placed under `Resources/Localization/<locale>.json`.
- **Resolution & Fallback**: The host resolves strings according to the active game language, falling back to `en-US` when a translation is unavailable.
- **Missing Keys**: If a key is missing in all dictionaries, the host localizer returns the raw key name rather than throwing exceptions or interrupting drawing.
- **Catalog Metadata vs. In-Game UI**: Catalog display names, summaries, and changelogs are defined in the index submission candidate; in-game UI strings are packaged inside the plugin ZIP.

---

## Adapting into Your Own Plugin

To turn this example into your own custom plugin:
1. **Change Identities**: Update `PluginPackageId`, `PluginDisplayName` and `AssemblyName` in `Example.csproj`, plus the module attribute and `ExtensionId`. `manifest.json` is generated, not manually edited. Declare compatibility/dependencies/external mods in `example/package-config.json`.
2. **Namespace & Assembly Name**: Rename `Phinix.Example.Basic` and update assembly outputs in `Example.csproj`.
3. **Settings Key Prefix**: Prefix all settings keys with your package ID (e.g., `myname.myplugin.settingKey`) to avoid colliding with other plugins.
4. **Publish & Submit**:
   - Create a public GitHub repository and publish an immutable GitHub Release containing your ZIP package.
   - Submit an issue to [Phinix-Plugin-Index](https://github.com/HunYuan2333/Phinix-Plugin-Index/issues/new/choose) following the [Submission Guide](https://github.com/HunYuan2333/Phinix-Plugin-Index#author-submission-guide).

## Split repository build inputs

Use the `Phinix-Rework` dev client checkout, initialized with `git submodule update --init --recursive`. The example references Utils through its pinned `Dependencies/Phinix.Common` checkout, not a neighboring floating Common branch. `pack.py` keeps the same public arguments:

```sh
python3 pack.py --phinix-root /path/to/Phinix-Rework --game-references /path/to/Phinix-Rework/GameDlls/1.6 --output /tmp/phinix-example.zip
```

The repository split does not change package/assembly identities or rewrite existing published releases.

For an installed host, add `--host-assemblies /path/to/phinix-rework/Common/Assemblies`. The packer recognizes numbered DLLs. Use `--packager /path/ManagedPackageTool.dll` to reuse a prepared tool. `pack.py` builds only this plugin and automatically runs offline ZIP and CLR-reference validation. No GitHub/token/Release is required for local work.

Enable RimWorld developer mode and choose Store → Install local plugin ZIP, then review and restart. Same-version different-digest development revisions are supported; identical digests and downgrades are rejected. Another source's installed package cannot be overwritten. Extension management remains available after developer mode is disabled and copies both installed and current-session build digests.

After building the host and this plugin, run the focused lifecycle regression:

```sh
dotnet run --project Tests/ExampleLifecycleTests.csproj -c Release -p:PhinixRoot=/path/to/Phinix-Rework
```

It exercises the actual compiled Example through ordinary discovery/DI for five cycles, checking settings, translation subscriptions, queued callbacks and borrowed-service ownership. It does not certify rendering or game save/disconnect behavior. Game acceptance remains required before unblocking skill work.

## Theme example

The local 1.0.3 candidate demonstrates ordinary module registration, role fallback, immutable draw snapshots and owned lease disposal. It targets current client API 2.0 and does not include a legacy branch. See [the theme example](Theme-Example.md). Online publication remains deferred.


## API semantic example

Local candidate 1.0.4 adds observable main-thread callbacks, cancellation and failure demonstrations using the public task scope. See [Api-Semantics-Example.md](Api-Semantics-Example.md). Requires the matching current host; no online release or game acceptance is claimed.


Local candidate 1.0.5 adds typed action extension points and an owned menu example. See Api-Semantics-Example.md and example/ExampleActions.cs. This is an experiment against the matching current host, without online/game acceptance.

## main/dev and official releases

Develop on dev; it triggers no Actions. Every accepted push to main reserves the next patch version in the configured major/minor series, builds against fixed client/Common source and private compile-only references, then publishes an official GitHub Release with the plugin ZIP, SHA256SUMS and source/build summary. Retrying the same commit reuses its reserved version; failed builds leave an unpublished draft for retry and never replace published bytes. Parallel pushes reserve distinct versions without canceling pending builds.

Maintainers configure BUILD_REFERENCES_TOKEN as a repository secret, with Contents:Read only on the private compile-reference repository named in ci/config.json. This is a maintainer CI setup; third-party authors supply their own licensed references. No game/host/Harmony DLL is uploaded in public artifacts. Assembly identity versions remain independently controlled by source; the automatically assigned package release version is passed to pack.py --version.

Merging into main means the author has accepted publication. A GitHub Release does not bypass Index admission/source-update policy or prove game acceptance. Test changes on dev before merging. Fixed host/reference inputs are maintained explicitly in ci/config.json. Do not overwrite released ZIPs or expose the reference token.
