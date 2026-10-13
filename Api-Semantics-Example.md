# Example API 语义样例

本地候选 1.0.5 要求本批完整当前客户端（未发布 API 2.0），不支持升级前 1.9，也不能混用早期 T3 SDK/Host。候选位于 Phinix/local-plugin-packages/2026-10-11-api。

## 接入点实验

ExampleActions.cs 定义 public ExampleActionContext 与 phinix.example.basic.actions，通过同一 owned scope 声明并贡献操作。页面“打开接入操作”展示计数、重置确认和零计数禁用；选择时 guard 检查 state/代次/计数。FloatMenu PostClose 释放结果集，Stop 撤销 scope。另一插件可引用实验 context 与点名调用 ContributeActions；这是公开 typed 接口，不反射 Example 私有视图。

当前只实验 Example 点，Chat/Trade 等真实视图接入仍待接续；不要求每个新 API 先有内置消费者。规范见 Phinix-Rework/docs/branch-local/dev/Plugin-Api-Integration-Standard.md。执行结果区分回调是否 Invoked 与是否 Completed，失败可能已有副作用，不自动重试。

## 本批实际展示

- Compose 只组合并注册 Tab/settings，Activate 绑定真实模块的主题、localizer 与 task scope；重复成功 Activate 不重复绑定。
- Host 服务借用；注册/Localizer/task scope 属于插件，自有 DI scope 释放状态和主题。部分激活失败也释放已取得的绑定。
- 计数仍同步写自己的设置。额外日志回调 Submit 后保存 handle，页面 Capture 显示 Queued/终态；Completed 仅指回调返回，不是远端确认。
- “演示取消”使用已经取消的 token，预期 RequestCanceled，回调不运行。
- “演示回调失败”只抛一个示例异常，预期 CallbackFailed 和诊断，后续动作不应被阻断。不涉及殖民地、物品或交易。
- Stop 清理任务 scope、localizer 订阅和最后 handle，旧确认回调仍检查 generation。主题缺键/语言刷新与保留计数规则保持。

Running 通常在两个 UI 绘制之间完成，不用阻塞主线程来展示它。只显示最后一次任务，不为制造 QueueFull 批量压入真实游戏队列；拒绝语义由契约/后续专项验收明确。

## 作为后续 API 样例

新增能力依次放真实可操作场景：provider 状态与缺依赖、读取自身资源、选定菜单/操作贡献、读取业务快照和提交意图。每项使用公开当前契约，不用反射私有 UI，不制造历史版本分支。尚未实现的能力不显示假成功，也不拿 Example 自己弹菜单替代真正跨插件注入。

Example 展示作者路径；实际内置消费者和真实业务失败/未知状态仍需对应切片核对。它不是第二套 Host 或通用测试框架。

## 检查记录

客户端和 Example 编译通过，本地包及 Host 引用静态校验通过。现有 fixture 仅对齐借用服务/scope mock，没有新增测试场景、构建或运行。游戏步骤未执行，UI 评估交接门槛仍未全部完成；线上发布延期。


## 1.0.6：provider 和模块 owned lifetime

同批当前客户端支持 IClientApiDirectory/IClientLifetimeService。Tab 的“查看 provider 与生命周期 API”打开实验窗口：正常应能取得主题及自身 Tab provider；刻意未注册的 optional contract 数量为 0。点击事件按钮增加回调，关闭 scope 后 token 取消/资源释放，再点击迟到回调不增加计数；重新绑定恢复，主题切换也触发 owned 订阅，关闭窗口立即释放。

provider State 是捕获元数据，TryGet 每次重核实际注册和激活；取得 API 借用、不 Dispose。lifetime scope/自有 resource/订阅拥有，module 停止由 Host 兜底。此演示不保存或操作殖民地数据，没有 1.9 分支；代码编译/候选打包，未运行游戏或自动化测试。

## 2026-10-11 UI 后续切片：字段、列表、只读区域与导航

Example 的 Host presentation 窗口现在消费同一公共 `ClientPresentationTextField` / `ClientPresentationList` / `ClientPresentationRegion`，演示动作标签筛选、行选择及只读进度条；行选择不会执行业务动作。计数、任务、重置仍使用原 typed action point，确认仍由同一个 Host renderer 绘制。

- 在 owner update 中通过 `session.TryDequeueInput` 读取 `TextChanged` / `RowSelected`，然后构建新 projection。输入携带 page revision；Publish、stale、close 和 stop 会清除旧输入。字段 validation/commit 归插件，Host 不解析数字或写 settings。
- field/list/row/region 使用稳定 ID。page 最多 32 fields、10000 rows（所有 section 合计），单字段 1–4096 字符长度约束；队列最多 32 未消费输入。列表按 cached prefix offsets 渲染可见行，主题/宽度/projection 改变才重新测量。
- `IClientPresentationRegion` 是 borrowed **只读**区域：measure/draw 只读、主线程、无文件扫描/订阅/命令；Host group clip 并恢复 GUI，区域异常只隔离区域。资源仍由 owner lifetime 管理。它尚不承诺交互式 Pawn/item 选择区或资产服务。
- `IClientNavigationMetadata.NavigationId` 是可选 local ID，Example tab 使用 `example`；Host 与 actual owner ID 组合，旧 Draw provider 使用 registration identity。没有 label 身份或官方入口白名单。
- `IClientViewLifecycle` 是可选 shell adapter；ViewOpened/ViewUpdate/ViewClosed 管 view 会话，不等于启动、取消或回滚业务。ViewUpdate 在 WindowUpdate 执行；游戏内生命周期仍待测。
- 同批 Host / abstractions / Example 才是候选批次。已有 1.0.7 ZIP 是此前基础 renderer 包，没有自动被此次源码修改更新；本次未发布/上传/改 Index。


## 2026-10-11 B3 / 本地 1.0.8：公共 composer

Host presentation 页消费 `ClientPresentationComposer`，便笺字段与显式 add 使用同一个 public renderer/typed action set。Enter 或按钮只排入 `SubmitRequested`；owner update 先 drain 全部输入到局部草稿，验证并 Publish 新 projection，然后 `InvokeAction` 执行当前 exact binding。不要在 drain 中改变 owner guard 的捕获值而丢弃同批 submit。失败不自动重试，关闭不取消业务；便笺只是本次演示状态，没有新增保存规则。

composer 的附加贡献动作通过 Host overflow 可达。icon 为借用 texture；focus/control name 由 Host session 管理。输入法组合中 Enter 不提交，此行为仍须实际游戏验证。本批客户端、Example 编译及本地 1.0.8 ZIP 静态/Host references 校验通过；游戏未运行。原 1.0.7 包不覆盖，1.0.8 必须配同批 Host/abstractions。无发布或 Index 更新。


## 2026-10-11 B4 / 本地 1.0.9：异步文本桥接

同一个 Host renderer 现在接收 TextField 在 Layout/Repaint 返回的真实文字差异，供 fcitx 等异步桥接使用；这些只进入 view draft/输入队列，不在 GUI 中执行计数或发送业务。Example 的普通 filter 字段与 composer 便笺同批获得修复，owner update 仍 drain→验证→Publish→显式 InvokeAction。文本重复 pass 不重复排队；Return 先交给 field，再检查消费状态、组合状态和是否在同次 Return 中提交文字。

本地候选 1.0.9 对同批 Host/abstractions 编译打包，不覆盖 1.0.8；真实 IME 和游戏焦点未测。Chat 新消息动作点 `builtin.chat.message.actions` 使用相同 typed contribution/Host presentation 能力；context 在 Chat Contracts，不是 Host 业务依赖。Example 没有为此引用 Chat 或增加第二套 renderer。


## 2026-10-11 B5 / 本地 1.0.10：共享视觉控件

同一 Host composer 使用新公共 `ClientWidgets.EmbeddedTextField` 和 `IconButton`；`RoundedSurface` / `RoundedBorder` 使用目标 Unity 的 public GUI.DrawTexture rounded overload。旧 TextField/Button 方法签名保留；所有 field 仍走标准 Widgets.TextField/validator，未绕过 IME patch。Example 的字段/列表/card/composer 全部直接获得共享样式，不包含私有皮肤或主题读取逻辑。icon 借用与 native dimensions 的原契约维持。

本地 1.0.10 只配同批 Host/abstractions 使用；候选编译与包静态检查不等于游戏视觉/IME 通过。字体、阴影、多 UI scale 与消息卡片仍需后续实施/合批游戏验收。


## B6 public feed example (1.0.11, game acceptance pending)

The example consumes ClientPresentationFeed/FeedRow in the same Host session as fields, lists, actions and composer. Its rows project the current counter and actual task state. Body/heading selection copies that state into the draft; reference selection calls ScrollToRow on the same session. Selection does not increment the counter or confirm a business operation. Feed/media events are owner intents, not callback success; borrowed textures remain owner-managed. Host and Example must use the same B6 API build. Build/packager checks passed; actual Verse GUI behavior remains pending.


B6 fix / 1.0.12: the Host view `⋯` entry consumes `session.CreateActionMenuOptions()` for a native FloatMenu using the exact published bindings. Keep the owner session alive while menu/confirmation is open. No separate action registry; reset confirmation and stale-context guards remain Host-owned. Game verification pending.


B7 / 1.0.13 (2026-10-12): rebuilt against the same shared widget and Host renderer batch. Shared buttons/navigation clip and align their own text; feed geometry is internally separated without a second session/registry. Native menu and existing public feed/form/composer remain the same API. Game layout and IME verification pending.


## B8 integer fields (1.0.14, 2026-10-12)

ClientPresentationTextField.Integer uses the same Host field renderer with an integer syntax hint. Empty/minus and overflow text remain drafts; the owner validates Int64/range/units. Example integer-demo displays Int64 validation without changing the counter or saving a business quantity. List capacity now permits 20000 rows (page aggregate 20000); individual feed remains 10000. Game verification pending. Inventory's public immutable action context and three PointId constants are in its Contracts, not referenced by Host.


## B9 wrapped actions and frozen summaries (1.0.15)

Section.WithWrappedActions returns a new immutable projection; Host measures and wraps controls within the same renderer. Example counter actions use it, and reset confirmation includes actual captured count/task text. Summary and confirmation text allow up to 65536 characters for complete transaction metadata; labels/fields remain separately bounded. Oversized confirmation is rejected rather than silently truncated. Neither selection, callback completion nor closing a view is business confirmation. Build/packager checks passed, game UI pending.


## B10 public split layout (1.0.16)

Page.WithSplitView(ClientPresentationSplitView) assigns the first N sections to the primary pane and the rest to details. Host uses two independently scrolling viewports above the declared width threshold and labelled pane tabs below it. Example fields/list occupy the primary pane; feed, counter and actions use the secondary pane. The composer remains shared at the bottom. One session retains input, action, confirmation, freshness and ownership guarantees. No plugin-owned private drawing is introduced. Build/package checks passed; game layout, glyphs and scaling remain pending.


## B11 feed metadata placement and selected actions (1.0.17)

FeedRow.WithMetadataPlacement(Right / AfterHeading) is an immutable visual projection; the original default remains Right. Both Example rows use real captured count metadata, with the first beside its heading and the second on the row edge. Host caches placement with its existing measurements. Action.WithSelection marks actual list selection without changing disabled reasons, owned action membership, confirmation or freshness. Shared accent marks and primary action styles use the existing theme registry; no second theme/renderer is created. Build/package passed; game glyphs and layout pending.


## B12 fixed headers and borrowed badges (1.0.18)

Split.WithFixedHeaders(global, primary, secondary, secondaryContextId) partitions existing sections into fixed and scrolling regions in the same Host session. The optional context ID resets detail scroll independently of list selection. Headers that exceed available height gain their own viewport; no new business confirmation or cancellation rule is introduced. Example keeps the counter actions fixed above the feed and uses actual contributed OwnerId badge text on its list. Row/Section.WithBadges projects up to four unique labels and optional borrowed Texture2D assets; the owner retains lifetime, Host renders icon/text with tooltip and label fallback. Badges and WrapActions compose independently. Source/build/packager passed; native GUI/texture decoding/game acceptance pending.


## B13 plain layout and grouped owned menus (1.0.19)

Section.WithPlainLayout removes the card surface and uses compact padding. Section.WithActionMenus groups existing section binding IDs into populated native FloatMenus; no foreign/duplicate/empty groups and no new callback registry. Option clicks still pass Request freshness/ownership/confirmation guards. Example consumes this with its counter actions and registry-colored owner badge. Split.WithAlignedHeaders can align scrolling body baselines. Badge.WithColor refers to an existing registry key; the Host does not infer trust, install source or business meaning. Source/build/package passed; real game visual/menu acceptance pending.


## B14 consistent responsive label measurement (1.0.20)

Host action/menu sizing uses ClientWidgets.MeasureButton with the same Small font, padding and separate arrow reserve as drawing. Width grows up to available space; toolbar wraps, and oversized labels gain height at a stable readable font size. Example uses a long translated tools label to exercise the shared menu. No private sizing/rendering or business changes. Stub/model/build/package checks passed; real font shaping and UI scale acceptance pending.


## B15 responsive shared layout pass (1.0.22)

Host composer height is based on actual label/icon/overflow size, with a stacked action row when inline space would squeeze input. Session.MeasureComposerHeight exposes the same pure layout to an owner docking a separate feed (used by Chat); full Example page consumes Host docking directly. Generic form helpers move actions below inputs when narrow; badges wrap and grow, feed metadata reserves measured Tiny text width, confirmations keep separate reachable scrolling content/action areas. Same public ownership, freshness, IME guards, theme and business authority remain. Build/package/non-GUI checks passed; real game acceptance pending.


## B16 feed metadata layout (1.0.23)

FeedRow.WithMetadataLayout(minimumWidth, gap) exposes bounded logical GUI dimensions (0..512 / 0..64; finite only). It composes immutably with WithMetadataPlacement in either order. Host retains container constraints, measured Tiny font, cached heading reservation, clipping and tooltip; no private downstream drawing. Example uses 72/12 and AfterHeading; Chat uses 64/12. Host draws complete metadata via explicit Tiny clipped label instead of native ellipsis. Real glyph/scale acceptance remains pending.


## B17 pinned single-column parity (1.0.24)

The same owned presentation action switches between the existing split view and Page.WithFixedHeaderSections(1). Single-column headers reuse Host pane measurement/rendering, short-window scrolling and current guards; no private fixed-coordinate renderer. Fixed headers require a remaining flow section and cannot conflict with SplitView. Compact shared buttons now use radius 8. Both languages demonstrate the switch; original fields, composer, feed, owned action sets and confirmation remain. Build/static package validation passed, real GUI acceptance pending.


## B19 theme presentation parity (1.0.26)

TextField.WithSlider(new ClientPresentationSlider(minimum, maximum, acceptedValue)) retains raw text and emits ordinary TextChanged. The owner parses invariant finite values and retains its last accepted preview on invalid text. Sliders require text fields and cannot attach to Integer or composer; GUI intent is not persistence. Section.WithSwatches supplies bounded passive RGBA samples rendered by the same Host. Example demonstrates both.

The permanent theme owner defines ClientThemePresentationActionContext.PointId through the same typed registry. Example contributes its own counter action only for Surface.Editor. Context carries read facts (theme/key/canApply/page/revision), not mutable draft or authority callbacks. Contribution stop and page freshness use ordinary scope/owned-set protections. Build/static references passed; native GUI/lifecycle gameplay pending.


## B20 action-point directory (1.0.27)

The existing public IClientExtensionPointService.CapturePoints directory is projected as ordinary section text, including real point ID, owner, context type and contributor count. This is registry state, not business authority or operation acknowledgement. The Example has no Trade Contracts/client assembly reference; absent optional points are absent from the directory. Original first controls section and fixed/split semantics remain. B20 Trade publishes its own readonly action context in Trade Contracts and consumes the same renderer; native game tests pending.


## B21 operation ownership (1.0.28)

The existing public section rendering adds localized hints: closing a view releases presentation resources; a submitted operation stays with the plugin; callback completion is not business confirmation. Controls, owned point directory, actions and confirmation stay on the same Host. Example deliberately adds no dependency on Trade Contracts/renderer internals and does not manipulate offer escrow. The Trade module publishes ITradeOfferOperationApi through normal API registration, with readonly current-session state and a guarded known local-settlement retry. Native and save/recovery tests remain pending.


## B24 read-only list (1.0.29)

ClientPresentationList.WithReadOnly() copies a list without a selection and suppresses row selection hit targets/hover on the same Host renderer. The original selectable list remains unchanged. Example displays counter/task projection facts through this property alongside its existing selectable action-list; readonly display does not disable text or grant operation authority. Trade quote inspection uses the same property; Example has no dependency on Trade Contracts. Source/build/static package passed; native visual behavior pending.


## B25 recovery facts (1.0.30)

Localized ownership hints now clarify that a read-only recovery record does not authorize retry or refund. Existing public readonly list/point directory remain the same. Trade publishes the normal ITradeInventoryRecoveryApi for current bound Inventory journal facts; consuming it requires Trade Contracts, which Example intentionally does not hard-reference. Capture/Revision enforce main-thread access, record values are readonly; no mutation port is offered. Native/save acceptance remains pending.

## B41 借用纹理来源

`task-fact` 演示 `WithIconSource(() => Texture2D.whiteTexture)`；普通插件可以借用RenderTexture，无需GPU读回。仅可见行Repaint调用，测量/布局不调用，Host不拥有纹理。来源应无业务副作用，owner管理失效/释放。当前Host需包含B41公共成员；Example候选1.0.35。生命周期fixture补齐既有ApiDirectory/Lifetime/Presentation依赖，45断言通过；它不执行真实GUI。
