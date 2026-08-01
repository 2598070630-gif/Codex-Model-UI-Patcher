# Codex Model UI Patcher

一个用于修补 ChatGPT/Codex 桌面端模型下拉框显示逻辑的小工具。

它的作用是取消 Codex 前端里对“隐藏模型”的白名单过滤，让 `ccswitch`、自定义模型映射或本地转换层暴露出来的模型，能够正常显示在模型选择列表里。

## 背景

在某些版本的 ChatGPT/Codex 桌面端中，后端实际已经能返回多个模型：

- `ccswitch /v1/models` 能看到多个模型
- `codex debug models` 能看到多个模型
- app-server 的 `model/list` 也能看到多个模型

但前端模型下拉框里可能只显示一个模型，例如 `gpt-5.5`。

原因不是转换层没有模型，而是前端打包代码里有一层隐藏模型过滤逻辑。当远端配置启用 `useHiddenModels` 后，前端只显示白名单里的模型，其他模型即使已经由后端返回，也不会展示出来。

这个工具会对桌面端的 `app.asar` 做一个很小的二进制补丁，把这层前端隐藏模型过滤关掉。

## 功能

- 自动查找当前安装的 `OpenAI.Codex` Microsoft Store 包
- 不写死版本号，Store 更新后仍然能定位新版本路径
- 修改前自动备份 `app.asar`
- 如果当前 Windows 会话不能直接替换 `WindowsApps` 里的文件，会自动安排到下次重启前替换
- 重复运行是安全的：如果已经补丁过，会提示已经是补丁版
- 生成日志，方便排查问题

## 使用方法

下载或编译 `CodexModelUIPatcher.exe` 后：

1. 关闭 ChatGPT/Codex 桌面端。
2. 双击运行 `CodexModelUIPatcher.exe`。
3. 接受 Windows 管理员权限提示。
4. 根据程序输出操作：
   - 如果提示“已立即替换”，重新打开 ChatGPT/Codex 即可。
   - 如果提示“已安排到下次 Windows 重启前替换”，重启 Windows 一次即可。

重启后不用再运行一次。

## Microsoft Store 更新后怎么办

Store 更新 ChatGPT/Codex 后，`app.asar` 通常会被新版覆盖。

这时只需要再次运行一次：

```powershell
CodexModelUIPatcher.exe
```

如果程序提示需要重启，重启一次即可。

## 补丁原理

当前已验证版本 (Codex 26.727.4816.0) 中，前端打包代码里隐藏模型过滤分散在 4 个位置。核心逻辑都是根据 `useHiddenModels` 和 `!hidden` 过滤模型列表，关键的三元表达式是：

```js
useHiddenModels && authMethod !== `amazonBedrock` ? availableModels.has(model) : !model.hidden
```

当 `useHiddenModels` 为 `true` 且不是 Bedrock 时（即 chatgpt 场景），会走前半段 `availableModels.has(model)` 白名单过滤。只改后半段 `!hidden` 是不够的——必须把**整个三元表达式**替换成 `!0`。

补丁器会对 4 个位置做等长二进制替换：

```text
1. J$r 函数:    (i&&t!==`amazonBedrock`?n.has(r.model):!r.hidden)   -> (!0<空格>   )
2. 非本地分支:  n.filter(e=>!e.hidden);                               -> n.filter(e=>!0<空格>);
3. catch 分支:  n.filter(e=>!e.hidden)}                               -> n.filter(e=>!0<空格>)}
4. 本地主过滤:  i.useHiddenModels&&r!==`amazonBedrock`?...:!e.hidden)} -> !0<空格>            )}
```

注意：替换时必须保留原位置的闭合括号（`(`、`)`、`}`），否则会导致 JavaScript 语法错误，Codex 卡在加载界面。

每处替换长度保持一致（`!0` 后用空格补齐），因此不会改变 `app.asar` 的整体大小和文件布局。

补丁器会检查每个位置：

- 支持"原始未补丁"和"旧版部分补丁"两种状态自动识别和转换
- 如果某位置的旧模式出现多次，避免误改，停止
- 如果全部位置都已是补丁版，提示无需操作
- 如果完全找不到可识别模式，提示需要更新补丁器

如果新版本前端代码结构发生变化，补丁器会停止并提示：

```text
未找到可识别的隐藏模型过滤代码
```

这时需要更新补丁器，而不是强行修改文件。

# 日志和备份

补丁器会把日志、备份和候选补丁文件放在 `CodexModelUIPatcher.exe` 所在目录：

```text
.\patcher.log
.\Backups
.\Candidates
```

如果 exe 位于项目的 `dist` 目录，常见路径示例：

```text
C:\Users\Administrator\Documents\Codex\2026-07-13\CodexModelUIPatcher\dist
```

其中：

- `patcher.log` 是运行日志
- `Backups` 目录里是修改前的 `app.asar` 备份
- `Candidates` 目录里是等待替换的补丁候选文件

如果需要清理旧版本遗留在 `%LOCALAPPDATA%` 或 `C:\ProgramData` 下的状态目录，可以右键以管理员身份运行 `cleanup-old-state-admin.cmd`。

## 编译

本项目可以使用 Windows 自带的 .NET Framework C# 编译器构建：

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe `
  /target:exe `
  /platform:anycpu `
  /win32manifest:CodexModelUIPatcher.exe.manifest `
  /out:CodexModelUIPatcher.exe `
  CodexModelUIPatcher.cs
```

`CodexModelUIPatcher.exe.manifest` 用于让程序启动时请求管理员权限。

## 故障排查

### 运行后仍然只显示一个模型

先确认后端是否真的返回了多个模型：

```powershell
codex debug models
```

如果后端只有一个模型，问题不在前端补丁。

如果后端有多个模型，但前端仍然只显示一个：

1. 完全退出 ChatGPT/Codex。
2. 重新打开应用。
3. 如果刚才补丁器提示需要重启，请先重启 Windows。

### 提示已经安排到重启前替换

这是正常情况。`C:\Program Files\WindowsApps` 下的 Microsoft Store 应用文件经常无法在当前会话里直接替换。

补丁器会调用 Windows 的重启前替换机制。重启一次后，系统会在应用启动前完成替换。

### 补丁后 Codex 卡在加载界面

这通常是补丁破坏了 `app.asar` 中 JavaScript 的语法结构，导致 Electron 无法加载前端代码。最常见的原因是补丁替换时吞掉了闭合括号（`(`、`)`、`}`）。

排查步骤：

1. 检查 `patcher.log` 中 `applied site` 的行，确认补丁器找到了几个位置
2. 用原生备份恢复 `app.asar`（在 `Backups` 目录中）
3. 检查补丁器源码中 `Newv` 字节数组末尾是否包含正确的闭合字符
4. 修复后重新编译并运行

如果不想手动排查，可以直接从 Microsoft Store 重新安装 Codex 恢复原生 `app.asar`。

### 提示找不到可识别的过滤代码

说明当前版本的 ChatGPT/Codex 前端打包代码可能已经变化。

这时不要手动乱改 `app.asar`，需要重新分析新版本前端代码并更新补丁器里的匹配字节。

## 风险说明

这个工具会修改 Microsoft Store 安装目录里的桌面端应用资源文件。

请注意：

- 这不是 OpenAI 官方工具
- Store 更新可能覆盖补丁
- 新版本前端结构变化时，补丁可能需要更新
- 使用前建议保留备份

补丁器会自动备份原始 `app.asar`，但你仍应自行承担修改本地应用文件带来的风险。

## 适用场景

适合以下情况：

- 你已经接入了 `ccswitch` 或类似模型映射
- 后端/API 层能看到多个模型
- Codex 桌面端模型下拉框只显示一个或少数模型
- 你希望前端直接显示转换层暴露出来的模型

不适合以下情况：

- 后端本身没有返回多个模型
- 你使用的不是 Microsoft Store 版 ChatGPT/Codex
- 你不希望修改本地应用安装文件

## License

MIT
