# Phinix 示例插件

<p align="center">
  <a href="./README.md">English</a> · 简体中文
</p>

Phinix 托管插件官方最小完备参考实现（包 ID: `phinix.example.basic`）。

> [!NOTE]
> - **技术指南**：关于公开 API、生命周期、DI 组合、线程与资源所有权规范，详见 [Phinix 插件开发指南](https://github.com/HunYuan2333/Phinix-Rework/blob/dev/docs/%E6%8F%92%E4%BB%B6%E5%BC%80%E5%8F%91%E6%8C%87%E5%8D%97.md)。
> - **实现状态**：开发源码已迁移 `ClientExtensionModule.Compose`，拥有插件 DI scope，借用宿主服务；既有正式 1.0.2 制品不改写。[本地开发闭环](https://github.com/HunYuan2333/Phinix-Rework/blob/dev/docs/Local-Plugin-Quickstart.md)不要求 GitHub、token 或 Release。
> - **构建状态**：GitHub Actions 云端自动构建（main 提交自动编译打包，dev 不编译）正在 F7 统一规划实施中；当前通过本地 `pack.py` 完成打包。
> - **Skill 说明**：Phinix 插件开发 skill 正在准备中，后续将补充链接。

---

## 概述与功能特性

本示例展示了托管插件的标准生命周期、UI 扩展模式以及打包发布全流程：
- **自定义多语言 Tab**：使用 `IMainTabProvider` 在 Phinix 窗口顶栏注册主页面。
- **配置持久化**：使用 `IClientSettingsContext` 读写带有包作用域前缀的用户设置。
- **交互状态与弹窗**：实现点击计数器、响应式布局提示与重置确认对话框。
- **宿主隔离本地化**：加载包内专属的中英文（`en-US.json`、`zh-CN.json`）语言文件。
- **安全执行**：**不包含任何白银或物品生成能力，不修改殖民地状态，亦不发起任何外部网络请求。**

---

## 玩家体验步骤

在游戏内测试此插件：
1. 打开游戏内 Phinix 窗口，切换至 **商店**（Store）Tab。
2. 找到 **Phinix 示例插件**，点击 **安装**（Install）。
3. **完全退出并重启 RimWorld** 以加载插件程序集。
4. 打开 Phinix 窗口：
   - 点击新增的 **示例**（Example）Tab 体验点击计数。
   - 打开 **设置**（Settings）→ **示例**，切换说明提示开关或测试重置计数器。
   - 在游戏选项中切换中英文语言，观察界面文本即时更新。
5. 在 **扩展管理**（Extension Manager）中测试停用或卸载该插件，重启游戏后确认干净清理。

---

## 源码地图

| 职责模块 | 对应源码 / 资源 | 说明 |
| :--- | :--- | :--- |
| **模块入口与生命周期** | `example/ExampleExtension.cs` (`ExampleExtension`) | 使用 `ClientExtensionModule.Compose`，拥有 DI scope，按通用生命周期启动和释放。 |
| **状态与设置管理** | `example/ExampleExtension.cs` (`ExampleState`) | 使用 `IClientSettingsContext` 存取键（如 `phinix.example.basic.clicks`）并监听语言变更事件。 |
| **Tab 页面绘制** | `example/ExampleExtension.cs` (`ExampleTab`) | 实现 `IMainTabProvider` 接口绘制响应式按钮与文本。 |
| **设置面板** | `example/ExampleExtension.cs` (`ExampleSettingsPanel`) | 实现 `IClientSettingsPanelProvider` 提供设置复选框与操作按钮。 |
| **双语本地化资源** | `example/Resources/Localization/{en-US,zh-CN}.json` | 包作用域的翻译字典，支持带占位符的参数格式化。 |
| **打包工具脚本** | `pack.py` | 调用官方 `ManagedPackageTool` 生成符合索引规范的发行 ZIP 包。 |

---

## 构建与打包

### 环境准备

- **.NET 10 SDK**（执行编译与打包工具）
- **本地 Phinix-Rework 源码**：用于引用 `Utils` 与 `ClientExtensionAbstractions`。
- **RimWorld 1.6 程序集引用**：仅用于编译的程序集（`Assembly-CSharp.dll`、`UnityEngine*.dll`）。严禁提交或随包分发。

### 快捷打包命令

在仓库根目录下执行：

```bash
python3 pack.py \
  --phinix-root <path-to-Phinix-Rework> \
  --game-references <path-to-RimWorld-Managed> \
  --output <path-to-output>/phinix-example-basic-1.0.2.zip
```

若需输出未压缩的开发文件夹供本地快速测试，可追加：
`--bundle-output <path-to-output>/phinix-example-basic/`

---

## 包体结构规范

标准的 Phinix 托管插件 ZIP 包内部目录树如下：

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

- **`manifest.json`**：包含包 ID、版本、声明程序集、兼容 Phinix 版本范围与依赖声明。
- **`Assemblies/`**：仅包含插件自身编译出的 DLL 文件。
- **`Resources/`**：包含插件专属的资源与语言字典。

> [!CAUTION]
> 压缩包内**绝对不能**包含 RimWorld 游戏程序集（如 `Assembly-CSharp.dll`）、宿主程序集（如 `Utils.dll`、`ClientExtensionAbstractions.dll`）或 Harmony。包含这些文件会导致运行时程序集冲突，并在索引准入静态校验时被直接拒绝。

---

## 本地化与回退机制

- **双语词典**：存放在 `Resources/Localization/<locale>.json`。
- **回退策略**：宿主根据当前游戏语言匹配词条；若当前语言未翻译，自动回退到 `en-US`。
- **缺词保护**：若某个词条在所有语言字典中均不存在，本地化服务直接返回原始 Key 字符串，避免抛出异常阻断渲染。
- **商店元数据 vs 游戏 UI**：商店目录中的多语言简介在提交 Issue 时定义；游戏内 UI 文案打包在插件 ZIP 内部。

---

## 改造为您自己的插件

将本示例改写为您自己的独立插件时，请注意以下关键点：
1. **修改唯一身份**：修改 `Example.csproj` 的 `PluginPackageId`、`PluginDisplayName`、`AssemblyName`，以及模块特性、`ExtensionId`。`manifest.json` 自动生成，不手改；兼容范围、依赖、外部 Mod 在 `example/package-config.json` 声明。
2. **命名空间与程序集**：重命名 `Phinix.Example.Basic` 并修改 `Example.csproj` 的输出程序集名称。
3. **设置键前缀**：所有配置键名必须带上自己的包 ID 前缀（如 `myname.myplugin.settingKey`），避免与其他插件冲突。
4. **发布与申请上架**：
   - 建立公开 GitHub 仓库，发布包含规范 ZIP 包的正式 GitHub Release。
   - 参照 [申请指南](https://github.com/HunYuan2333/Phinix-Plugin-Index/blob/main/README.zh-CN.md#%E6%8F%92%E4%BB%B6%E4%BD%9C%E8%80%85%E6%8F%90%E4%BA%A4%E6%8C%87%E5%8D%97) 前往 [Phinix-Plugin-Index](https://github.com/HunYuan2333/Phinix-Plugin-Index/issues/new/choose) 提交收录表单。

## 拆仓后的构建输入

使用 `Phinix-Rework` 的 dev 客户端 checkout，先执行 `git submodule update --init --recursive`。示例从客户端固定的 `Dependencies/Phinix.Common` 引用 Utils，不引用旁边浮动的 Common 分支。`pack.py` 参数保持原样：

```sh
python3 pack.py --phinix-root /path/to/Phinix-Rework --game-references /path/to/Phinix-Rework/GameDlls/1.6 --output /tmp/phinix-example.zip
```

拆仓不改包/程序集身份，也不重写已有发布物。

已安装宿主可添加 `--host-assemblies /path/to/phinix-rework/Common/Assemblies`，打包脚本会识别带数字前缀的 DLL。`--packager /path/ManagedPackageTool.dll` 复用已构建工具，`--version 1.0.3` 保留 main 发行的版本注入。`pack.py` 只编译当前插件，并自动执行离线 ZIP/实际 CLR 引用预检。

开启 RimWorld 开发者模式，从商店“安装本地插件 ZIP”导入并重启。允许同版本不同摘要开发修订，拒绝重复摘要、降级和覆盖其他来源。关闭开发者模式后，已安装开发包仍可启停/卸载，扩展管理可复制当前和已安装构建摘要。

宿主与示例编译后执行 `dotnet run --project Tests/ExampleLifecycleTests.csproj -c Release -p:PhinixRoot=/path/to/Phinix-Rework`，验证实际 Example 五轮发现/DI 激活/关闭、设置保留、翻译订阅、过期回调与借用服务不被释放。控制台回归不能替代游戏切档、断线与 UI 验收；完成游戏验收前 skill 继续暂停。

## 主题示例

本地 1.0.3 候选增加普通模块主题注册、缺键回退、绘制快照和 scope 释放示例。要求当前 API 2.0，不含旧客户端分支。详见 [主题示例](Theme-Example.md)。在线发布仍延期。


## API 语义样例

本地候选 1.0.4 用公开任务 scope 展示回调状态、取消和失败；详见 [API 语义样例](Api-Semantics-Example.md)。要求本批完整当前 Host，未在线发布或游戏验收。


本地候选 1.0.5 新增强类型操作接入点与 owned 菜单实验，见 Api-Semantics-Example.md 与 example/ExampleActions.cs；要求本批完整当前 Host，未在线/游戏验收。
