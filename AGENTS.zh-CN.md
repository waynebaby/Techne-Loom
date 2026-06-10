# Workspace Agent Rules（中文镜像）

[English](AGENTS.md)

> `AGENTS.md` 是仓库自动化执行规则的规范源文件。
> `AGENTS.zh-CN.md` 是中文镜像，必须与英文文件保持同步；若两者冲突，以 `AGENTS.md` 为准。

## 共享 Python 环境

- 这个工作区通过 `.venv.path` 使用共享虚拟环境指针。
- 该环境由 `cto-skills-manager` 管理。
- 在 Windows 上，调用 Python 类工具前，先用 PowerShell 解析 `.venv.path`。
- 如果 Python 运行时可用，但 `.venv.path` 指向的环境还不存在，先初始化该虚拟环境，再继续后续工作。
- 在 Linux 上，调用 Python 类工具前，先用 bash 解析 `.venv.path`。

## 运行输出命名

- 当某个 skill 创建按次运行的输出根目录时，保留 skill 自己的父目录，并将本次输出根命名为 `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`。
- 时间戳必须紧跟在 `exec-` 后面，保证即使相邻步骤切换 skill，输出目录依然按时间可排序。

## 仓库方向

- Techne Loom 是一个 `.NET` 优先、面向多生态发布的 mono-repo，会在 `.NET`、Node.js、Python 三个生态维护平行包系。
- `AgentOrchestrator` 和 `SkillOrchestrator` 是两个生态位不同的独立产品。它们不会互相调用，也不能被描述成父子运行时关系。
- 低层抽象可以对齐，但包身份、发布身份、产品对外契约必须保持独立。

## 包与目录布局

- 从一开始就按语言根目录组织源码：`/src/dotnet`、`/src/nodejs`、`/src/python`。
- 每个项目单元都必须是一个可发布包。
- 在 `.NET` 中，每个 `.csproj` 都对应一个 NuGet 包或 `dotnet tool` 包。
- 在 Node.js 中，每个带独立 `package.json` 的 package 目录都对应一个 npm 包。
- 在 Python 中，每个带独立 `pyproject.toml` 的包/构建单元都对应一个 PyPI distribution 或 wheel。
- 各生态里的包系按角色保持平行：`abstractions`、`common`、`agent-orchestrator`、`skill-orchestrator`。

## 文档与语言规则

- 对外文档默认全部双语。
- 文档树保持 `/docs/zh-cn` 与 `/docs/en` 镜像结构。
- 每一对双语页面都必须在页首提供互相链接。
- 根目录必须维护双语文件：`README.md`、`CONTRIBUTING.md`、`CHANGELOG.md`、`SECURITY.md`、`AGENTS.md`。
- 根目录英文文件保留默认文件名，中文镜像统一使用 `.zh-CN.md` 后缀。
- 根目录双语文件也应该在页首互链。
- `AGENTS.md` 只保留在仓库根，不在 `/docs` 下复制。
- 产品 guide 的源文档固定放在 `/docs/<lang>/reference/products/ao-guide.md` 与 `/docs/<lang>/reference/products/so-guide.md`。
- `dotnet ao.dll --guide` 与 `dotnet so.dll --guide` 必须输出与当前版本匹配、可离线使用、由精选文档源生成的 guide 内容。
- 根目录的 package 获取索引固定为 `packages.released.md`、`packages.released.zh-CN.md`、`packages.beta.md`、`packages.beta.zh-CN.md`，skills 应通过绝对 GitHub URL 引用它们。
- 这些 package 获取索引除了包管理器安装命令外，还必须提供托管在 GitHub 上的 stable / beta 最新 release fallback 下载链接。
- MCP、CLI、skill 输入/输出契约文档属于一等交付物，不能只散落在 README prose 里。

## Workflow 术语规则

- 整个 repo 的 workflow 术语根文档固定放在 `/docs/en/architecture/workflow-terminology.md` 与 `/docs/zh-cn/architecture/workflow-terminology.md`。
- AO / SO 的解释性 prose、guide、README，以及后续 schema 说明都要以这份术语表为准。
- 解释“向外交出控制权”和“带结构化结果回来继续”时，优先使用 **weave out** 与 **weave back**。
- 在 repo 文档里优先使用 **strand**，不要用 **thread**，避免和 `.NET` threading 术语冲突。
- 用 **seam** 表达概念层的所有权接缝；**boundary** 保留给显式的 wire / protocol surface，例如 `boundary_reason` 和 `<so_property>` 块内 `type: "boundary"` 的 envelope。
- 当解释性术语与当前实现字段名不一致时，第一次出现必须把两者都写清楚，并保留真实字段名。
- 任何产品文档若要引入新的 workflow 隐喻，必须先同步更新术语表及其双语镜像。

## README 定位

- 把 `README.md` 和 `README.zh-CN.md` 当成旗舰 landing page，而不只是技术索引。
- 有意识地使用 GitHub 支持的 rich Markdown：badges、alerts/callouts、对比表、Mermaid 图、架构图、用途场景和强定位文案。
- 文案可以强包装，但所有主张都必须能被当前实现和文档支撑。
- 当需要刷新术语、生态位描述或概念包装时，先做有边界的研究；必要时使用 `cto-web-research` 再改写 README 叙事。

## Guide 输出规则

- `dotnet so.dll --guide` 与 `dotnet ao.dll --guide` 默认输出完整 Markdown，支持 section 过滤，支持 `--lang zh-cn|en`，并支持 `--export <path>`。
- Guide 页首应包含版本、构建号与兼容性元数据。
- Guide 必须覆盖行为、职责、契约、模板、示例和反模式。
- Guide 既要适合人阅读，也要适合模型直接 ingest；当需要稳定抽取时，使用 `guide-contract`、`guide-template`、`guide-checklist`、`guide-example` 这类 fenced block 标签。
- Guide 与 reference 内容要显式列出 MCP 方法、CLI 参数、planner 流程、审计 artifact 路径，以及 skill 输入/输出 payload 形状。

## 审计 Artifact 规则

- Workflow 审计输出不是可选展示辅助，而是按步骤保存的审计记录。
- 除非用户明确指定审计输出目录，否则默认使用临时输出根目录。
- 审计产物按 `{output}/wf-{wfid}/step-{seq}-{action}/` 落盘。
- 每个 step 目录都必须包含该时刻的 Mermaid Markdown、HTML 和 workflow JSON 备份。

## 执行顺序与评审提交节奏

- 在更大范围实现前，先更新 `AGENTS.md` 与 `AGENTS.zh-CN.md`，确保语言、文档和执行规则是最新的。
- 每完成一个大的实现切片，都必须先跑一次合理的 `cto-review-and-commit` review/fix/validate/commit 流程，再进入下一个切片。
- 这个节奏默认是硬 gate，不是软建议：除非用户明确覆盖，否则不要跨越多个大切片一路累计更改，最后再一次性 review。
- 每次 review-and-commit 的切片要保持在“有证据可审”的规模内。默认规划规则是：单次切片通常应控制在 50 个变更文件以内；如果待提交范围已经接近这个规模，就先停下来跑 `cto-review-and-commit`，不要继续堆更多文件。
- 即使文件数还不到 50，只要这一切片涉及协议契约、schema、包接缝或运行时控制行为变化，也要立刻进入 `cto-review-and-commit`。
- 大切片包括：根 AGENTS 规则、旗舰 README landing page、文档骨架、包骨架、协议/schema 变更以及代码实现。
- 除非用户明确覆盖这一节奏，否则不要带着未评审、未提交的更改直接进入下一个大切片。
