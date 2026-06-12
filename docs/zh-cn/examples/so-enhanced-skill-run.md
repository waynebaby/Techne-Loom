# SO 增强 Skill 运行示例

[English](../../en/examples/so-enhanced-skill-run.md) | [根目录](../README.md)

这个示例展示的是一个通用化的 SO 增强 target skill 运行过程。它的重点不是某个技术领域本身，而是 SO 如何把一条复杂运行路线从输入到完成都保持在正确轨道上。

> [!NOTE]
> 本页刻意隐藏产品领域细节、厂商细节、仓库私有锚点与本地文件名。重点是说明：当任务足够大、足够容易漂移时，SO 如何用治理路线把运行保持正确。

## 建议配套阅读

- [SkillOrchestrator Guide](../reference/products/so-guide.md)
- [Skills 参考](../reference/skills.md)
- [Workflow 术语](../architecture/workflow-terminology.md)
- [Skill 驱动 Workflow 示例](skill-driven-workflow.md)

## 场景速览

这个运行从一个常见工程请求开始，并且必须收束成一份 implementation-ready 的输出包，同时保留可追踪、可复核、可恢复、可审计的特性。

如果没有治理，这类运行通常会沿着四种路径漂移：

- scope 接收得过于宽松
- branch 探索深浅不一
- 证据不足就提前开始 synthesis
- 文档看起来完成了，但没有正式审计闭环

SO 的作用就是在这些漂移发生之前，把运行重新压回一条显式路线。

## 路线图

```mermaid
flowchart TD
    A[请求 intake] --> B{最小输入是否齐备?}
    B -- 否 --> B1[阻塞 seam\n要求补足 scope]
    B -- 是 --> C[Preflight 与环境确认]
    C --> D[输入归一化]
    D --> E[结构化 branch fan-out]
    E --> E1[第一性原理分支]
    E --> E2[参考分支 A]
    E --> E3[参考分支 B]
    E1 --> F[批判与冲突审查]
    E2 --> F
    E3 --> F
    F --> G{是否已有足够证据进入 synthesis?}
    G -- 否 --> E
    G -- 是 --> H[权威 synthesis]
    H --> I{验证是否通过?}
    I -- 否 --> J[修复并重新验证]
    J --> H
    I -- 是 --> K[正式 evidence handoff]
    K --> L[治理运行完成]
```

## 为什么路线能保持正确

```mermaid
sequenceDiagram
    participant Caller as 调用方 / Outer Agent
    participant SO as SO Runtime
    participant Branches as Branch 分析
    participant Validator as 验证关口
    participant Audit as 审计与证据

    Caller->>SO: 用 runtime workflow copy 执行 run
    SO->>SO: 进行输入 gate 与 preflight 确认
    SO->>Branches: 发起结构化 branch fan-out
    Branches-->>SO: 返回 branch 输出与 critique 结果
    SO->>Validator: 提交 synthesis 候选
    Validator-->>SO: 通过或失败
    alt 验证失败
        SO-->>Caller: 返回阻塞修复路线与当前 workflow 状态
        Caller->>SO: 用结构化修复证据 resume
        SO->>Validator: 再次验证
    end
    Validator-->>SO: 返回已验证输出
    SO->>Audit: 产出 event log、workflow backup、Mermaid、HTML
    Audit-->>Caller: 返回正式 completion evidence
```

## 分阶段叙述

### 1. 输入 gating

SO 会先停住运行，直到最小输入被确认。这一步很早就完成了三件事：

- 把模糊 scope 变成显式约束
- 把缺失假设变成真实默认值或真实决定
- 在重分析开始前先稳定请求上下文

### 2. Preflight 与环境确认

SO 要求在主路线继续前先确认环境与执行上下文。这不仅是工具可用性检查，更是路线保护，避免在未验证基础上开始大段分析。

### 3. 输入归一化

上游原始材料先被转换成适合下游消费的标准化工件。这样后面的 branch、批判、synthesis 都能围绕同一套 canonical inputs，而不是混杂原始碎片继续推进。

### 4. 结构化 branch fan-out

运行不会只走一条线性设计路线，而是明确拆成第一性原理分支、多个 reference-style 分支，以及每个分支的 critique。SO 让这些 branch 以可比较的结构落地后，才允许后续继续。

### 5. 先 critique 再 synthesis

SO 不允许把 branch 生成误当成最终设计。每个 branch 都必须先经过批判，弱假设和冲突点会先暴露，再决定是否进入 synthesis。

### 6. 权威 synthesis

只有 branch evidence 与 critique 已经齐备时，SO 才允许进入 synthesis。这使最终输出不再是“第一个看起来可行的答案”，而是来自前序证据的显式归纳。

### 7. 验证关口

验证是硬 gate，不是礼貌步骤。只要权威输出还没通过结构与完整性要求，运行就不能宣布完成。

### 8. 正式 evidence handoff

真正的完成不是一句 “done”。SO 要求运行以正式证据集收尾，这样后续任何人都能在不依赖聊天记忆的前提下重建这条路线。

## SO 实际产出了什么

| 表面 | 贡献了什么 | 为什么重要 |
| --- | --- | --- |
| Workflow state | 持久化、阶段感知的控制路径 | 防止静默漂移 |
| Event log | 按 step 记录的执行历史 | 让运行可审计 |
| Mermaid + HTML renders | 某一时刻的 workflow 可视化 | 让进度和恢复更容易解释 |
| Blocked seams | 带 resume 要求的结构化暂停 | 让恢复保持确定性 |
| Completion evidence | 正式 done-state handoff | 把“看起来完成”与“官方完成”区分开 |

## Evidence 形状

一个成功的治理运行最终会留下这样一组小而关键的证据：

- 当前 workflow state 或终态 workflow state
- 追加式 event log
- 某一时刻的 Mermaid Markdown 与 HTML render
- 每个审计 step 的 workflow JSON backup
- 与权威输出绑定的 validation pass 信号

正因为有这组证据，结果才不仅是“完成”，而且还是可复核、可恢复、可追责的。

## 为什么这个模式可复用

只要一个 skill 需要做到下面这些事，这条路线就值得复用：

- 把模糊输入转成结构化输出包
- 在选择前比较多条设计路径
- 保存 critique 和 conflict resolution
- 在 closure 之前验证主要输出
- 以可审计 completion evidence 结束运行

真正可复用的资产不是某个领域里的内容，而是这条被治理过的执行形状。

## 推荐定位方式

请把这个示例定位为：

- workflow governance 示例
- traceability 示例
- structured synthesis 示例
- completion discipline 示例

不要把它定位成某个领域的设计参考，否则就会重新引入本页刻意隐藏掉的技术细节。

## 关键结论

- SO 最重要的价值，是控制路线质量，而不只是帮你更快地产出内容。
- 运行之所以保持正确，是因为每一步进展都必须先通过 gate。
- branching、critique、synthesis、validation、evidence closure 属于同一条官方路径。
- 被治理过的运行更强，正是因为它不能绕过结构。

## 继续阅读

- 返回 [示例目录](README.md)
- 阅读运行时契约：[SkillOrchestrator Guide](../reference/products/so-guide.md)
- 阅读 skill 层规则：[Skills 参考](../reference/skills.md)
