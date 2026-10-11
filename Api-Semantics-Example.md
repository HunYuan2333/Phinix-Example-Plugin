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
