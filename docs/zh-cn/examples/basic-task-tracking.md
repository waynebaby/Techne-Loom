# 基础 Task Tracking 示例

[English](../../en/examples/basic-task-tracking.md) | [根目录](../README.md)

这个示例展示了最小但值得保留的公开 workflow 形状。

## 流程

1. 一个 workflow instance 从某个 state node 开始。
2. 一个确定性 transition 更新 context。
3. 一个 history entry 记录这次移动。
4. 命令输出会被捕获进 workflow context 和最终 result payload。

## 最小 Workflow File

```json
{
	"instanceId": "sample1",
	"nodes": {
		"state.start": {
			"$kind": "state",
			"id": "state.start",
			"name": "Start",
			"workflowPhase": "01 Intake",
			"groups": [
				{
					"id": "group.main",
					"strategy": "firstSuccess",
					"transitionIds": ["transition.run"]
				}
			],
			"waitBehavior": "blockUntilComplete"
		},
		"state.done": {
			"$kind": "state",
			"id": "state.done",
			"name": "Done",
			"workflowPhase": "03 Complete",
			"groups": [],
			"waitBehavior": "blockUntilComplete"
		},
		"transition.run": {
			"$kind": "command",
			"id": "transition.run",
			"name": "Run tool",
			"targetNodeId": "state.done",
			"outputPath": "toolResult",
			"stepKind": "toolCall",
			"guardExpression": "true",
			"succeedExpression": "true",
			"command": {
				"kind": "tool",
				"name": "echo",
				"parameters": {
					"message": "hello"
				},
				"currentRetryCount": 0
			},
			"currentRetryCount": 0,
			"maxRetry": 10
		}
	},
	"startNodeId": "state.start",
	"currentNodeId": "state.start",
	"endNodeId": "state.done",
	"status": "readyToStart",
	"context": {},
	"history": [],
	"version": 0,
	"activeWaitGroups": []
}
```

## 运行命令

```powershell
.\so.exe run --workflow-file .\workflow.json
```

## 预期结果

- SO 会执行确定性的 `toolCall` 步骤。
- workflow 会在 `state.done` 结束。
- 最终 `<so_property>` 块会携带一个 `result` payload。


## 执行图

```mermaid
flowchart LR
    A["🧭 state.start<br/>开始状态"] --> B["⚙️ toolCall: echo<br/>工具调用"]
    B --> C["🧾 context.toolResult<br/>上下文结果"]
    C --> D["✅ state.done<br/>完成状态"]

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef runtime fill:#dbeafe,stroke:#2563eb,color:#1e3a8a;
    classDef evidence fill:#ede9fe,stroke:#7c3aed,color:#4c1d95;
    classDef done fill:#dcfce7,stroke:#15803d,color:#14532d;
    class A intake;
    class B runtime;
    class C evidence;
    class D done;
    subgraph legend["Legend<br/>图例"]
        L1["🧭 intake / 接入"]
        L2["⚙️ runtime / 运行时"]
        L3["🧾 evidence / 证据"]
        L4["✅ completion / 完成"]
    end
    class L1 intake;
    class L2 runtime;
    class L3 evidence;
    class L4 done;
```

## Compile 证据

这个 workflow JSON 示例面向 SO runtime。请把它放到 skill 目录之外的 external workflow copy，再用同版本 runtime 执行下面的命令；返回的 Mermaid 和 audit 文件必须留在临时输出根目录。checked-in 示例是解释层 source，实际运行 copy 生成的 Mermaid 才是执行 authority。

```powershell
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```


## Runtime 生成的 Mermaid（SO 0.3.316-beta）

来源 workflow：`basic-task-tracking.md` 的第一个 JSON workflow block。命令：`so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>`。

```mermaid

flowchart TD
    subgraph phase_01_intake["01 Intake"]
    state.start["⚙️ Start"]
    end
    subgraph phase_03_complete["03 Complete"]
    state.done["📜 Done"]
    end
    state.start -->|Run tool| state.done
    style state.done fill:#f8fafc,stroke:#94a3b8,stroke-width:1px
    style state.start fill:#dbeafe,stroke:#2563eb,stroke-width:1px
    style state.start stroke:#ea580c,stroke-width:3px
    subgraph legend[Legend]
        legend_ai["🔎 AI"]
    style legend_ai fill:#dcfce7,stroke:#16a34a,stroke-width:1px
        legend_tool["⚙️ Code/Tool"]
    style legend_tool fill:#dbeafe,stroke:#2563eb,stroke-width:1px
        legend_branch["❓ Conditional branch"]
    style legend_branch fill:#fef3c7,stroke:#a16207,stroke-width:1px
        legend_optional["💬 Optional user choice"]
    style legend_optional fill:#fef3c7,stroke:#d97706,stroke-width:1px
        legend_required["🚧 Required user input"]
    style legend_required fill:#fee2e2,stroke:#dc2626,stroke-width:1px
        legend_gate["📜 Gate"]
    style legend_gate fill:#f8fafc,stroke:#94a3b8,stroke-width:1px
    end

```
