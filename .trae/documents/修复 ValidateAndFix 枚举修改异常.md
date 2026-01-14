# 原因定位
- 报错点在 [GameBoardView.ValidateAndFix](file:///d:/unity/Works/Match3-Demo/Assets/Scripts/Match3/GameBoardView.cs#L113-L177) 第 117 行：`foreach (var kv in _slots)`。
- 循环体内多次执行 `_slots[pos] = view;` 以及 `SyncSlotVisual(...)` 后再次访问 `_slots[pos]`。
- 在 C# 的 `Dictionary<TKey,TValue>` 中，只要对字典进行写入（即便是给已存在 key 更新 value），都会使枚举器失效，因此会抛出 `InvalidOperationException: Collection was modified`。

# 修复策略
## 1) ValidateAndFix 改为“快照 keys 再遍历”
- 在方法开头生成一份 key 快照：`var keys = ListPool<GridPosition>.Get(); keys.AddRange(_slots.Keys);`
- 迭代 `keys`，每次用 `_slots.TryGetValue(pos, out var view)` 取当前值，再根据需要写回 `_slots[pos] = view`。
- 最后 `keys.Release2Pool()`。
- 这样遍历的是 List，不会因为字典写入导致枚举器失效。

## 2) ForceResyncAll 同样修复
- [ForceResyncAll](file:///d:/unity/Works/Match3-Demo/Assets/Scripts/Match3/GameBoardView.cs#L179-L197) 也在 `foreach (_slots)` 中写回 `_slots[pos]`，潜在同类问题；同样改为 keys 快照遍历。

# 验证方式
- 重新编译检查诊断（应不再出现该异常）。
- 进入 Play：启动时不再抛 `Collection was modified`。
- 打开 `enableValidation`：仍能自动修复 mismatch，且不会再引入枚举异常。