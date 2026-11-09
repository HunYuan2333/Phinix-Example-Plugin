# Relay 测试面板

分支为 codex/command-relay-test-panel。本分支配合 codex/command-capability-relay 的候选 Framework 使用。正式发布 Host pin 和插件版本仍沿用原值，本次候选验证的实际 Host 来自本地 Relay 工作树。

## 打开与准备

1. 用候选 Host 构建本分支 Example，在测试客户端启用该插件并重启。
2. 打开 Phinix 的“示例”页，点击“客户端通信测试”。
3. 同一服务器准备 A、B、C 三个客户端。A、B 使用本分支 Example，C 可保持所有能力关闭以验证过滤。
4. 登录后查看能力同步状态及本机玩家 ID，点击“刷新在线玩家”。选择玩家会填入目标 ID，也可以手动输入离线 ID。

基础通信由 Framework Server 提供。测试发送与接收两端使用客户端插件。服务端 Interceptor 专项另需临时测试服务端插件。

## 面板操作

| 操作 | 行为 |
| --- | --- |
| 声明/撤销 request | 动态持有或释放 request 能力句柄 |
| 声明/撤销 announcement | 独立控制 announcement 能力 |
| 版本 | 用于下一次声明和发送，必须为正整数。已经声明的实际版本显示在状态区 |
| 修改已声明版本 | 先撤销，修改版本，再声明。修改输入框不会隐式更改现有声明 |
| 消息类型 | 常用 request、announcement。其他类型仍按显式 Relay 发出，接收方需声明对应能力 |
| 发送给指定玩家 | 使用目标 ID、消息类型、版本与原始载荷单播 |
| 按能力广播 | 省略玩家目标，Framework 按相同能力匹配并排除发送连接 |
| 生成新测试编号 | 生成带唯一 caseId 的 JSON，便于核对收发记录 |
| 切换接收处理模式 | 在正常、暂停处理、抛出测试异常之间切换。声明状态独立保留 |
| 发送超限载荷 | 通过公共 Relay API 提交超过 64 KiB 的测试内容，观察 PayloadTooLarge |
| 连续广播 25 条 | 执行一次有上限的突发发送，用于观察 RateLimited 和恢复 |
| 清空记录 | 清除本地显示记录，声明与待决操作继续存在 |

状态区显示 SyncState、SyncError、本机 ID、两个实际声明版本、接收模式和等待结果数量。记录包含发送输入、RequestMessageId、路由 Status/Error、逐连接 Outcome，以及实际接收的发送者 ID、消息类型、版本和载荷。最多保留 50 条，每条详情最多 4096 字符。

Submitted 表示网络层接受提交。接收端出现 receive 记录后，才有该测试插件接收的观察证据。插件按原样记录载荷，研究等具体业务需由业务插件实现。

## 第一轮黑盒步骤

1. A、B 登录后等待 Synced。在 B 声明 request v1，A 保持发送版本为 1。
2. A 选择 B，生成 caseId，点击单播。对照 A 的 result 和 B 的 receive，检查 ID、发送者、类型、版本与载荷。
3. B 撤销 request，A 再单播，应得到 NoCapability。B 离线时应得到 TargetOffline。
4. B 声明 announcement v1，C 不声明。A 把消息类型改为 announcement 并广播，只有 B 接收。
5. B 撤销 announcement，广播应返回 NoRecipients。恢复声明后可再次收到。
6. B 改声明 request v2，A 发 v1 应被拒绝。A 改发 v2 后可投递。
7. B 保留声明并切换到暂停处理，A 仍可能得到 Submitted，B 不增加 receive。切换到异常模式会触发 Framework 的异常隔离，切回正常后继续收发。
8. A 点击超限载荷及 25 条突发发送，查看明确错误，稍后再发送正常消息检查恢复。
9. 关闭面板，再打开，检查声明与最近记录保持。断线重连后同步恢复，再次验证单播及广播。
10. 停止插件或重启后，确认旧回调不产生新记录，按当前模块管理要求重新启用并声明能力。

旧版客户端排除、新客户端连接旧服务端、双连接玩家、长时间负载及服务端 Interceptor 专项参照 Host 的 R5 清单。这个面板通过公共客户端 API 提供正常测试入口。

## 构建与验证

Example 构建只引用已经编译的 Host。可以通过已有 PhinixRoot、GameReferences 或逐程序集路径参数指定候选。

```sh
dotnet build example/Example.csproj -c Release -p:PhinixRoot=/absolute/path/to/built-host -p:GameReferences=/absolute/path/to/GameDlls
dotnet build Tests/ExampleLifecycleTests.csproj -c Release -p:HostAssemblies=/absolute/path/to/phinix-rework/Common/Assemblies -p:GameReferences=/absolute/path/to/GameDlls
dotnet Tests/bin/Release/net10.0/ExampleLifecycleTests.dll
```

已使用候选的 1.6/net472 引用编译，并用实际 1.7 引用编译 net10 源码图。白盒测试覆盖能力声明与撤销、声明版本、单播和广播参数、暂停/异常处理、记录上限、超限测试输入、停止清理、借用服务所有权及迟到结果。游戏内实际布局与联机黑盒留待运行候选后验收。

缺少 Relay 公共契约的正式 Host 不适合加载本测试分支。正式发布前需先完成候选 Host/Common 的验收与真实提交，再按仓库流程同步 Host pin、版本和候选包指纹。

## 2026-11-10 候选交付核对

用户反馈黑盒全部通过，并授权交叉与压力以白盒完成验收。候选分支 ci/config.json 固定 Host 提交 9c0c5aecd8dd6214bf4d6427f0879fc74de2d28e，其 Common 为 911047cf8a6dd34e661e107e182e9c921b8e9586。最终候选 Host 上 Example 生命周期 140 条断言、五轮激活/关闭通过，两版编译通过。此固定仅提交到候选分支；不推送 main、不触发正式发行、不修改版本或 Index。此前段落的待验收/未更新 pin 描述为历史状态，以本节为当前状态。
