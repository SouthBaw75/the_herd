using System;

namespace CattleRanch.Sim
{
    /// <summary>
    /// An immutable, value-like calendar date derived entirely from a monotonic
    /// day counter (<see cref="TotalDays"/>). Day 0 == Year 1, Spring,
    /// DayOfSeason 1. Because every field is a pure function of
    /// <see cref="TotalDays"/>, a date is fully described by that single number
    /// (which is all that needs serializing).
    /// </summary>
    public sealed class GameDate : IEquatable<GameDate>
    {
        /// <summary>Days in one season. Config constant, not a buried literal.</summary>
        public const int DaysPerSeason = 28;

        /// <summary>Seasons in one year.</summary>
        public const int SeasonsPerYear = 4;

        /// <summary>Days in one year (28 × 4 = 112).</summary>
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

        /// <summary>The starting date of a new game: Day 0.</summary>
        public static GameDate Start => new GameDate(0);

        public GameDate(int totalDays)
        {
            if (totalDays < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalDays), "totalDays must be non-negative.");
            }

            TotalDays = totalDays;
        }

        /// <summary>Monotonic tick counter; the single source of truth for the date.</summary>
        public int TotalDays { get; }

        /// <summary>1-based year.</summary>
        public int Year => (TotalDays / DaysPerYear) + 1;

        /// <summary>Current season.</summary>
        public Season Season => (Season)((TotalDays % DaysPerYear) / DaysPerSeason);

        /// <summary>1-based day within the current season.</summary>
        public int DayOfSeason => (TotalDays % DaysPerSeason) + 1;

        /// <summary>Returns a new date advanced by <paramref name="days"/> days.</summary>
        public GameDate AddDays(int days) => new GameDate(TotalDays + days);

        public bool Equals(GameDate? other) => other != null && other.TotalDays == TotalDays;

        public override bool Equals(object? obj) => Equals(obj as GameDate);

        public override int GetHashCode() => TotalDays;

        public override string ToString() =>
            $"Year {Year}, {Season}, Day {DayOfSeason} (total {TotalDays})";
    }
}
