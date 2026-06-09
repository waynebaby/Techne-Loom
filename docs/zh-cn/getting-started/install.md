# 安装与运行

[English](../../en/getting-started/install.md)

## 环境要求

- .NET SDK 9 或更高版本
- Git
- 能执行 `dotnet` 的本地 shell

## 仓库布局

- `src/dotnet` 承载首个可运行实现。
- `src/nodejs` 与 `src/python` 预留给未来兼容移植。
- `docs` 保存双语作者文档源文件。

## 首批命令

```powershell
dotnet restore
dotnet build Techne.Loom.sln
dotnet test Techne.Loom.sln
```

如果 solution 还没有 restore，请先在仓库根目录执行 `dotnet restore`。

## 第一个真实 smoke 命令

build 成功后，最短的真实 SO 命令路径是：

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- ls .
```

预期输出形状：

```xml
<wrapped_exec>
<commandline>...</commandline>
<exectionstream>
...目录列举输出...
</exectionstream>
</wrapped_exec>
<so_property>
{"type":"result", ...}
</so_property>
```

这条命令一次性证明四件事：

- SO CLI 可运行
- shorthand 扩展有效
- wrapped command output 与 SO control payload 分离
- completed 结果路径会以 machine-readable 形式输出
