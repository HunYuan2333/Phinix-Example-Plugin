# 普通插件主题示例

Example 1.0.3 本地候选要求当前 API >=2.0.0 <3.0.0，无 1.9 兼容分支。ExampleTheme 借用 IClientTheme/IClientThemeRegistry；Compose 仅组合对象，Activate 以实际模块实例注册 phinix.example.basic namespace、暂存颜色并 Commit。注入成功不等于激活成功。

- panel 回退 ui.surface，counter 回退 ui.accent，均有真实消费点。
- Tab/设置每次绘制 Capture；控件用同一不可变 reader 并恢复 GUI 状态。
- 主题缺键按角色/注册默认值显示；未知键由 Host 保存，插件不判断主题版本或文件 schema。
- ExampleTheme 在自有 DI scope 释放时撤销注册；借用的 Host 服务不 Dispose。部分注册失败显式释放 owned lease，整个激活失败按现有 Shutdown 释放 scope。
- 不注册 Change 事件，不需要为主题刷新拥有另一份业务状态；计数、提示设置和主线程回调继续使用原 ExampleState。

当前公共 UI API 尚未正式冻结，未来重构可以改视觉实现；这不阻止现在使用普通主题注册路径开发。本地编译和包静态校验通过，未运行游戏/自动化验收。原生命周期 fixture 只补齐 borrowed theme mocks，没有新增测试场景或运行测试。
