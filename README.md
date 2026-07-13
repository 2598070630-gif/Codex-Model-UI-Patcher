# Codex Model UI Patcher
纯vibecoding

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

当前已验证版本中，前端打包代码里存在如下逻辑：

```js
u=s&&e!==`amazonBedrock`
```

后续列表过滤会根据 `u` 决定是否只展示白名单模型。

补丁器会把这段等长替换为：

```js
u=false                 
```

替换长度保持一致，因此不会改变 `app.asar` 的整体大小和文件布局。

如果新版本前端代码结构发生变化，补丁器会停止并提示：

```text
没找到可识别的过滤代码
```

这时需要更新补丁器，而不是强行修改文件。

## 日志和备份

补丁器会把日志和备份放在：

```text
%LOCALAPPDATA%\CodexModelUIPatcher
```

常见路径示例：

```text
C:\Users\<用户名>\AppData\Local\CodexModelUIPatcher
```

其中：

- `patcher.log` 是运行日志
- `Backups` 目录里是修改前的 `app.asar` 备份

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
