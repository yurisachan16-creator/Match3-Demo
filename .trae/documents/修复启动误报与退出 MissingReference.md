# 问题原因

## (1) 启动时 “BoardView mismatches fixed: 64”
- `GameController.Start` 里在 `_game.StartAsync().Forget()` 之后立刻调用 `boardView.ValidateAndFix()`。
- 但 `StartAsync` 的填充/入场动画是异步执行的：填充阶段 `BeginBatch` 会延迟视图刷新，导致这时 **数据已填充、视图还没生成 ItemView**。
- `ValidateAndFix(rebuildMissingItems=true)` 会在每格创建 ItemView，并把这 64 次“修复”当作 mismatch 记录，从而误报。
- 同时它会把 `transform.position` 强行拉回 AnchorWorld，可能覆盖掉正在进行的入场动画，因此你看到“入场动画消失”。

## (2) 退出 PlayMode 时 MissingReferenceException
- 退出时 Unity 会销毁 GameObject，但 `UniTask` 动画协程仍在跑，继续访问 `go.transform` 就会触发 `MissingReferenceException`。

# 修复方案

## A) 把启动流程改成真正等待填充结束
- 将 `GameController.Start` 改为启动一个 `StartGameAsync()`：
  - 使用 `this.GetCancellationTokenOnDestroy()` 作为生命周期 token
  - `await _game.StartAsync(token)`，并捕获 `OperationCanceledException`（退出时不再打印异常堆栈）
  - **在 StartAsync 完成后**再执行 `ValidateAndFix()`，此时不会再出现 64 的误报，也不会打断入场动画。

## B) 动画方法绑定销毁 token + 空引用防护
- 在 `GameBoardView.AnimateSwapAsync/AnimateClearAsync/AnimateMovesAsync/AnimateSpawnAsync` 内：
  - 把传入的 `ct` 与 `GetCancellationTokenOnDestroy()` 链接（linked token）
  - 每帧访问 `go.transform` 前先判断 `go == null`（Unity Destroy 后会变成“伪 null”），若为空则直接 return
  - `await UniTask.Yield(..., token)` 使用 linked token，确保退出时能立即取消。

## C) RebuildBoard/Swap 的 token 统一
- `TrySwapAsync` / `RebuildBoardAsync` 也使用同一个生命周期 token，以避免退出或销毁时仍有后台任务继续运行。

# 验证方式
- 进入 PlayMode：不再出现启动时 `mismatches fixed: 64`；入场下落动画恢复。
- 退出 PlayMode：不再出现 `MissingReferenceException`。
- 打开 `enableValidation`：只在真实不同步时才出现 mismatch 统计。

确认后我会按以上方案修改 `GameController.cs` 与 `GameBoardView.cs` 并重新检查诊断。