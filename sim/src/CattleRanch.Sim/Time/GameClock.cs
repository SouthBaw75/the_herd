using System;

namespace CattleRanch.Sim
{
    /// <summary>
    /// The deterministic, tick-based clock. The atomic tick is one day.
    /// <see cref="Tick"/> advances the date and raises <see cref="SeasonChanged"/>
    /// / <see cref="YearChanged"/> when those boundaries are crossed. Presentation
    /// code subscribes to the events; the simulation itself never ticks from
    /// Unity's frame <c>Update()</c>.
    /// </summary>
    public sealed class GameClock
    {
        public GameClock(GameDate? start = null)
        {
            Date = start ?? GameDate.Start;
        }

        /// <summary>The current date.</summary>
        public GameDate Date { get; private set; }

        /// <summary>Raised when a <see cref="Tick"/> moves into a new season, carrying the new season.</summary>
        public event Action<Season>? SeasonChanged;

        /// <summary>Raised when a <see cref="Tick"/> moves into a new year, carrying the new (1-based) year.</summary>
        public event Action<int>? YearChanged;

        /// <summary>Advances exactly one day, firing boundary events as crossed.</summary>
        public void Tick()
        {
            GameDate previous = Date;
            GameDate next = previous.AddDays(1);
            Date = next;

            if (next.Season != previous.Season)
            {
                SeasonChanged?.Invoke(next.Season);
            }

            if (next.Year != previous.Year)
            {
                YearChanged?.Invoke(next.Year);
            }
        }

        /// <summary>Advances <paramref name="days"/> days; identical to calling <see cref="Tick"/> that many times.</summary>
        public void Advance(int days)
        {
            if (days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "Cannot advance a negative number of days.");
            }

            for (int i = 0; i < days; i++)
            {
                Tick();
            }
        }
    }
}
