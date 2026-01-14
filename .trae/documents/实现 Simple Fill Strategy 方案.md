# 简单填充策略 (Simple Fill Strategy) 实现方案

我已确认您的方案规划非常完整且符合软件工程规范。基于 Match3SDK 的特性，我为您细化了 **“技术设计”** 和 **“实现阶段”** 的具体执行计划：

## 1. 核心类与接口设计 (对应技术设计阶段)

我们将创建以下核心类来实现简单填充逻辑：

*   **`GameSlot` (数据模型)**
    *   实现 `IGridSlot` 接口。
    *   新增 `int ItemId` 属性存储棋子类型。
    *   新增 `bool HasItem` 属性用于判断是否为空。
*   **`SimpleFillStrategy` (策略类)**
    *   实现 `IBoardFillStrategy<GameSlot>` 接口。
    *   **核心职责**：根据当前棋盘状态，生成填充任务 (Job)。
    *   **逻辑**：
        *   `GetFillJobs`: 游戏开始时，对所有格子生成填充任务。
        *   `GetSolveJobs`: 消除发生后，仅对被消除的格子生成填充任务（原地再生，不产生掉落）。
*   **`SimpleFillJob` (任务类)**
    *   实现 `IJob` 接口。
    *   **核心职责**：执行实际的数据填充操作，将空格子设置为新的随机 ItemId。
*   **`IItemsPool` (辅助工具)**
    *   用于提供随机的 ItemId，避免硬编码。

## 2. 实施步骤 (对应实现阶段)

我们将分三步完成代码编写：

### 第一步：基础数据结构
创建 `GameSlot.cs` 和 `ItemsPool.cs`。
- `GameSlot` 需要包含清除数据 (`Clear`) 和设置数据 (`SetItem`) 的方法。

### 第二步：策略与任务实现
创建 `SimpleFillStrategy.cs` 和 `SimpleFillJob.cs`。
- `SimpleFillJob` 将遍历传入的格子列表，为每个空格子从 Pool 中获取一个新 ID 并赋值。

### 第三步：集成与验证
创建 `GameController.cs` 作为入口。
- 组装 `Match3Game` 实例。
- 注入 `SimpleFillStrategy`。
- 编写简单的测试代码（如点击按钮模拟消除），验证被消除的格子是否立即变回随机颜色。

## 3. 补充说明 (关于集成验证)
为了让您能直观看到策略生效，我会在 `GameSlot` 中添加简单的 `OnItemChanged` 事件，并在 `GameController` 中使用 Unity 的 `Gizmos` 或 `Debug.Log` 来打印棋盘状态，暂时不涉及复杂的 UI 动画，以便专注于策略逻辑的验证。
