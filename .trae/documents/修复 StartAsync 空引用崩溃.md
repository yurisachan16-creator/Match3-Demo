# 问题定位

控制台的 `NullReferenceException` 指向 `BaseGame.StartGame()` 的 `_levelGoals` 为空：
- `BaseGame.StartGame()` 在启动时会遍历 `_levelGoals` 并订阅事件（见 [BaseGame.cs](file:///d:/unity/Works/Match3-Demo/Library/PackageCache/com.chebanovdd.match3sdk@0.1.3/Runtime/Match3.App/Internal/BaseGame.cs#L47-L61)）。
- `_levelGoals` 只有在调用 `InitGameLevel(level)` 后才会被赋值（见同文件 [BaseGame.cs](file:///d:/unity/Works/Match3-Demo/Library/PackageCache/com.chebanovdd.match3sdk@0.1.3/Runtime/Match3.App/Internal/BaseGame.cs#L36-L45)）。
- 目前 [GameController.cs](file:///d:/unity/Works/Match3-Demo/Assets/Scripts/Match3/GameController.cs#L16-L44) 直接调用了 `_game.StartAsync()`，但没有先调用 `_game.InitGameLevel(...)`，因此运行期必然 NRE。

# 修复方案

## 1. 在 GameController.Start 中初始化关卡
- 在 `_game = new MyMatch3Game(config);` 之后、`StartAsync()` 之前调用：
  - `_game.InitGameLevel(0);`
- 这会创建棋盘槽位，并从 `ILevelGoalsProvider` 获取关卡目标数组，保证 `StartGame()` 不会访问到空引用。

## 2. 保障 LevelGoalsProvider 永远不返回 null
- 保留现有实现 `return new LevelGoal<GameSlot>[0];`，确保即使没有目标也返回空数组而不是 null。

## 3. 运行期异常更易定位（可选但推荐）
- 将 `_game.StartAsync().Forget();` 改为带异常回调的 `Forget`（不改变逻辑，只让异常更早更清晰地打印），避免异常被 UniTask 的 unobserved handler 延后。

# 验证步骤

1. 修改完成后检查编辑器编译错误（确保零 error）。
2. 进入 Play Mode：确认 Console 不再出现 `BaseGame.StartGame` 的空引用异常。
3. 使用 Inspector 上的 `Test Swap` 验证交换流程仍可触发日志。
