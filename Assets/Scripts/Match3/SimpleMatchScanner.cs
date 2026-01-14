using System.Collections.Generic;
using Match3.App.Interfaces;
using Match3.Core.Structs;

namespace Match3.App.Demo
{
    public static class SimpleMatchScanner
    {
        public static void CollectMatchedPositions(IGameBoard<GameSlot> board, HashSet<GridPosition> matched)
        {
            matched.Clear();

            for (int r = 0; r < board.RowCount; r++)
            {
                int runStart = 0;
                int runId = -1;
                int runLen = 0;

                for (int c = 0; c < board.ColumnCount; c++)
                {
                    var slot = board[r, c];
                    int id = slot.HasItem ? slot.ItemId : -1;

                    if (c == 0)
                    {
                        runStart = 0;
                        runId = id;
                        runLen = 1;
                        continue;
                    }

                    if (id != -1 && id == runId)
                    {
                        runLen++;
                    }
                    else
                    {
                        if (runId != -1 && runLen >= 3)
                        {
                            for (int cc = runStart; cc < runStart + runLen; cc++)
                            {
                                matched.Add(new GridPosition(r, cc));
                            }
                        }

                        runStart = c;
                        runId = id;
                        runLen = 1;
                    }
                }

                if (runId != -1 && runLen >= 3)
                {
                    for (int cc = runStart; cc < runStart + runLen; cc++)
                    {
                        matched.Add(new GridPosition(r, cc));
                    }
                }
            }

            for (int c = 0; c < board.ColumnCount; c++)
            {
                int runStart = 0;
                int runId = -1;
                int runLen = 0;

                for (int r = 0; r < board.RowCount; r++)
                {
                    var slot = board[r, c];
                    int id = slot.HasItem ? slot.ItemId : -1;

                    if (r == 0)
                    {
                        runStart = 0;
                        runId = id;
                        runLen = 1;
                        continue;
                    }

                    if (id != -1 && id == runId)
                    {
                        runLen++;
                    }
                    else
                    {
                        if (runId != -1 && runLen >= 3)
                        {
                            for (int rr = runStart; rr < runStart + runLen; rr++)
                            {
                                matched.Add(new GridPosition(rr, c));
                            }
                        }

                        runStart = r;
                        runId = id;
                        runLen = 1;
                    }
                }

                if (runId != -1 && runLen >= 3)
                {
                    for (int rr = runStart; rr < runStart + runLen; rr++)
                    {
                        matched.Add(new GridPosition(rr, c));
                    }
                }
            }
        }
    }
}

