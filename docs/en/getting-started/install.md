# Install And Run

[中文](../../zh-cn/getting-started/install.md)

## Requirements

- .NET SDK 9 or later
- Git
- A local shell capable of invoking `dotnet`

## Repository Layout

- `src/dotnet` contains the first runnable implementation.
- `src/nodejs` and `src/python` are reserved for later compatible ports.
- `docs` contains the bilingual authored source docs.

## Expected First Commands

```powershell
dotnet restore
dotnet build Techne.Loom.sln
dotnet test Techne.Loom.sln
```

If the solution has not been restored yet, start with `dotnet restore` at the repository root.

## First Real Smoke Command

After the build succeeds, this is the shortest real SO command path:

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- ls .
```

Expected shape:

```xml
<wrapped_exec>
<commandline>...</commandline>
<exectionstream>
...directory listing...
</exectionstream>
</wrapped_exec>
<so_property>
{"type":"result", ...}
</so_property>
```

That command proves four things at once:

- the SO CLI is runnable
- shorthand expansion works
- wrapped command output is separated from SO control payloads
- a completed result path is emitted in machine-readable form
