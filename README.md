# Phinix Example Plugin

<p align="center">
  English · <a href="./README.zh-CN.md">简体中文</a>
</p>

The official minimal reference implementation for authoring, packaging, and publishing Phinix managed plugins (Package ID: `phinix.example.basic`).

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
| **Module Entry & Lifecycle** | `example/ExampleExtension.cs` (`ExampleExtension`) | Implements `IPhinixExtensionModule` and `IActivatablePhinixExtensionModule`. Handles registration and teardown. |
| **State & Settings Management** | `example/ExampleExtension.cs` (`ExampleState`) | Stores persisted keys (e.g., `phinix.example.basic.clicks`) and manages localization events. |
| **Tab UI Rendering** | `example/ExampleExtension.cs` (`ExampleTab`) | Implements `IMainTabProvider` to draw responsive buttons and text. |
| **Settings Panel** | `example/ExampleExtension.cs` (`ExampleSettingsPanel`) | Implements `IClientSettingsPanelProvider` to render settings checkboxes and action buttons. |
| **Dual-Language Resources** | `example/Resources/Localization/{en-US,zh-CN}.json` | Package-scoped translations with placeholder format arguments. |
| **Packaging Tool** | `pack.py` | Invokes the official `ManagedPackageTool` to generate schema-compliant release ZIPs. |

---

## Build & Packaging

### Prerequisites

- **.NET 10 SDK** (runs compilation and packaging tools)
- **Local Phinix-Rework Source**: Used to reference `Utils` and `ClientExtensionAbstractions`.
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
1. **Change Identities**: Update the package ID (e.g., `myname.myplugin`) in `Example.csproj`, `manifest.json`, and all `[PhinixExtension("...")]` attributes.
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

## main/dev and official releases

Develop on dev; it triggers no Actions. Every accepted push to main reserves the next patch version in the configured major/minor series, builds against fixed client/Common source and private compile-only references, then publishes an official GitHub Release with the plugin ZIP, SHA256SUMS and source/build summary. Retrying the same commit reuses its reserved version; failed builds leave an unpublished draft for retry and never replace published bytes. Parallel pushes reserve distinct versions without canceling pending builds.

Maintainers configure BUILD_REFERENCES_TOKEN as a repository secret, with Contents:Read only on the private compile-reference repository named in ci/config.json. This is a maintainer CI setup; third-party authors supply their own licensed references. No game/host/Harmony DLL is uploaded in public artifacts. Assembly identity versions remain independently controlled by source; the automatically assigned package release version is passed to pack.py --version.

Merging into main means the author has accepted publication. A GitHub Release does not bypass Index admission/source-update policy or prove game acceptance. Test changes on dev before merging. Fixed host/reference inputs are maintained explicitly in ci/config.json. Do not overwrite released ZIPs or expose the reference token.
