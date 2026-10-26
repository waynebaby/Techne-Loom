# 更新日志

[English](CHANGELOG.md)

本文件记录仓库的所有重要变更。

项目当前仍处于首个公开版本前的开源抽取阶段。在第一个正式标签发布前，日志条目主要记录仓库成形过程中的关键里程碑。

## Unreleased

- 修正 Windows 本地产物 URL，改用标准的三斜杠 `file:///C:/...` 形式，替代无效的双斜杠 `file://C:/...` 形式——后者把盘符放在 URL authority 位置，导致宿主无法打开。Linux/macOS 的 URL 原本即符合标准，保持不变。
- 在 `AGENTS.md` 与 `AGENTS.zh-CN.md` 中建立双语仓库执行规则。
- 在 `README.md` 与 `README.zh-CN.md` 中建立旗舰落地页。
- 开始搭建公开文档树，并编写 AO/SO guide 源文档。
