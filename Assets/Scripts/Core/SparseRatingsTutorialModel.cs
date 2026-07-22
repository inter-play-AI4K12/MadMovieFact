namespace MadFact
{
    /// <summary>
    /// Level 4's final, deliberately sparse ratings table. Three predictions are the
    /// average of two closest rows or columns. Two widely separated exceptions show why
    /// even a sensible collaborative-filtering shortcut cannot cover every person.
    /// </summary>
    public sealed class SparseRatingsTutorialModel
    {
        public const int Rows = 5;
        public const int Columns = 9;

        public static readonly string[] CustomerNames =
            { "WENDELL", "DOT", "HANK", "PRIYA", "TIBBS TWINS" };

        public static readonly string[] MovieNames =
        {
            "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN", "QUASAR RUN", "DEMOLITION DAVE",
            "MIDNIGHT MANSION", "LAUGH TRACK", "CITY HEARTS", "WILD PLANET"
        };

        public readonly int[,] Target =
        {
            { 5, 4, 2, 5, 3, 1, 2, 4, 1 },
            { 2, 3, 5, 2, 5, 3, 5, 2, 4 },
            { 5, 4, 2, 5, 3, 1, 2, 4, 1 },
            { 5, 4, 2, 5, 3, 1, 2, 4, 1 },
            { 1, 2, 2, 1, 2, 5, 4, 3, 5 }
        };

        // Ratings the customers actually supplied. False cells were never rated.
        public readonly bool[,] Known =
        {
            { false, true,  true,  false, true,  true,  true,  true,  false },
            { true,  false, true,  false, true,  true,  false, false, true  },
            { true,  false, true,  false, true,  false, true,  true,  false },
            { true,  true,  false, false, true,  true,  false, false, true  },
            { false, true,  true,  false, false, true,  false, true,  false }
        };

        // The two exceptions occupy opposite ends of the table: (0,8) and (4,0).
        public readonly bool[,] Task =
        {
            { false, false, false, false, false, false, false, false, true  },
            { false, false, false, false, false, false, true,  false, false },
            { false, true,  false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, true,  false },
            { true,  false, false, false, false, false, false, false, false }
        };

        readonly bool[,] _hintUsesRows =
        {
            { false, false, false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, false, false },
            { false, true,  false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, true,  false },
            { false, false, false, false, false, false, false, false, false }
        };

        readonly bool[,] _exception =
        {
            { false, false, false, false, false, false, false, false, true  },
            { false, false, false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, false, false },
            { true,  false, false, false, false, false, false, false, false }
        };

        public SparseRatingsTutorialModel()
        {
            // Normal tasks exactly use the two-neighbour average. The exception targets
            // are intentionally far from that average, not merely off by one star.
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    if (!Task[row, column]) continue;
                    float average = SuggestedAverage(row, column);
                    Target[row, column] = IsException(row, column)
                        ? (average <= 3f ? 5 : 1)
                        : UnityEngine.Mathf.RoundToInt(average);
                }
        }

        public bool HintUsesRows(int row, int column) => _hintUsesRows[row, column];
        public bool IsException(int row, int column) => _exception[row, column];

        /// <summary>Returns the two visible rows or columns closest to the selected task.</summary>
        public int[] HintIndices(int row, int column)
        {
            return HintUsesRows(row, column)
                ? ClosestRowIndices(row, column)
                : ClosestColumnIndices(row, column);
        }

        public float SuggestedAverage(int row, int column)
        {
            int[] pair = HintIndices(row, column);
            return HintUsesRows(row, column)
                ? (Target[pair[0], column] + Target[pair[1], column]) * 0.5f
                : (Target[row, pair[0]] + Target[row, pair[1]]) * 0.5f;
        }

        public int[] ClosestRowIndices(int selectedRow, int selectedColumn)
        {
            return ClosestPair(Rows, selectedRow, candidate =>
            {
                if (!Known[candidate, selectedColumn]) return float.MaxValue;
                float sum = 0f;
                int overlap = 0;
                for (int column = 0; column < Columns; column++)
                {
                    if (column == selectedColumn || !Known[selectedRow, column] || !Known[candidate, column])
                        continue;
                    sum += UnityEngine.Mathf.Abs(Target[selectedRow, column] - Target[candidate, column]);
                    overlap++;
                }
                return overlap >= 2 ? sum / overlap + 0.01f / overlap : float.MaxValue;
            });
        }

        public int[] ClosestColumnIndices(int selectedRow, int selectedColumn)
        {
            return ClosestPair(Columns, selectedColumn, candidate =>
            {
                if (!Known[selectedRow, candidate]) return float.MaxValue;
                float sum = 0f;
                int overlap = 0;
                for (int row = 0; row < Rows; row++)
                {
                    if (row == selectedRow || !Known[row, selectedColumn] || !Known[row, candidate])
                        continue;
                    sum += UnityEngine.Mathf.Abs(Target[row, selectedColumn] - Target[row, candidate]);
                    overlap++;
                }
                return overlap >= 2 ? sum / overlap + 0.01f / overlap : float.MaxValue;
            });
        }

        static int[] ClosestPair(int count, int excluded, System.Func<int, float> score)
        {
            int first = -1, second = -1;
            float firstScore = float.MaxValue, secondScore = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                if (index == excluded) continue;
                float value = score(index);
                if (value < firstScore)
                {
                    second = first;
                    secondScore = firstScore;
                    first = index;
                    firstScore = value;
                }
                else if (value < secondScore)
                {
                    second = index;
                    secondScore = value;
                }
            }
            if (first < 0 || second < 0)
                throw new System.InvalidOperationException("Sparse rating task needs two visible neighbours.");
            return new[] { first, second };
        }

        public int TaskCount
        {
            get
            {
                int count = 0;
                for (int row = 0; row < Rows; row++)
                    for (int column = 0; column < Columns; column++)
                        if (Task[row, column]) count++;
                return count;
            }
        }
    }
}
