using System.Collections.Generic;
using CattleRanch.Sim;

namespace CattleRanch.Sim.Tests
{
    public class GameClockTests
    {
        [Fact]
        public void Start_IsYear1_Spring_Day1()
        {
            var date = GameDate.Start;
            Assert.Equal(0, date.TotalDays);
            Assert.Equal(1, date.Year);
            Assert.Equal(Season.Spring, date.Season);
            Assert.Equal(1, date.DayOfSeason);
        }

        [Fact]
        public void YearIs112Days_FourSeasonsOf28()
        {
            Assert.Equal(28, GameDate.DaysPerSeason);
            Assert.Equal(4, GameDate.SeasonsPerYear);
            Assert.Equal(112, GameDate.DaysPerYear);
        }

        [Fact]
        public void Advancing112Days_ReturnsToSpring_Year2_Day1()
        {
            var clock = new GameClock();
            clock.Advance(GameDate.DaysPerYear); // 112

            Assert.Equal(112, clock.Date.TotalDays);
            Assert.Equal(2, clock.Date.Year);
            Assert.Equal(Season.Spring, clock.Date.Season);
            Assert.Equal(1, clock.Date.DayOfSeason);
        }

        [Fact]
        public void SeasonBoundaries_AreAtDay28_56_84()
        {
            var clock = new GameClock();

            clock.Advance(27);
            Assert.Equal(Season.Spring, clock.Date.Season); // day 28 of Spring (DayOfSeason 28)
            Assert.Equal(28, clock.Date.DayOfSeason);

            clock.Tick(); // TotalDays 28
            Assert.Equal(Season.Summer, clock.Date.Season);
            Assert.Equal(1, clock.Date.DayOfSeason);

            clock.Advance(28); // TotalDays 56
            Assert.Equal(Season.Fall, clock.Date.Season);

            clock.Advance(28); // TotalDays 84
            Assert.Equal(Season.Winter, clock.Date.Season);
        }

        [Fact]
        public void SeasonChanged_FiresExactlyFourTimesPerYear_AtBoundaries()
        {
            var clock = new GameClock();
            var seasonsSeen = new List<Season>();
            var seasonChangeDays = new List<int>();
            clock.SeasonChanged += s =>
            {
                seasonsSeen.Add(s);
                seasonChangeDays.Add(clock.Date.TotalDays);
            };

            clock.Advance(GameDate.DaysPerYear); // one full year

            Assert.Equal(4, seasonsSeen.Count);
            Assert.Equal(new[] { Season.Summer, Season.Fall, Season.Winter, Season.Spring }, seasonsSeen);
            Assert.Equal(new[] { 28, 56, 84, 112 }, seasonChangeDays);
        }

        [Fact]
        public void YearChanged_FiresOncePerYear_AtDay112()
        {
            var clock = new GameClock();
            var yearsSeen = new List<int>();
            var yearChangeDays = new List<int>();
            clock.YearChanged += y =>
            {
                yearsSeen.Add(y);
                yearChangeDays.Add(clock.Date.TotalDays);
            };

            clock.Advance(GameDate.DaysPerYear * 3); // three years

            Assert.Equal(new[] { 2, 3, 4 }, yearsSeen);
            Assert.Equal(new[] { 112, 224, 336 }, yearChangeDays);
        }

        [Fact]
        public void Advance_EqualsRepeatedTick()
        {
            var advanced = new GameClock();
            var ticked = new GameClock();

            advanced.Advance(365);
            for (int i = 0; i < 365; i++)
            {
                ticked.Tick();
            }

            Assert.Equal(advanced.Date.TotalDays, ticked.Date.TotalDays);
            Assert.Equal(advanced.Date, ticked.Date); // value equality
            Assert.Equal(advanced.Date.Year, ticked.Date.Year);
            Assert.Equal(advanced.Date.Season, ticked.Date.Season);
            Assert.Equal(advanced.Date.DayOfSeason, ticked.Date.DayOfSeason);
        }

        [Fact]
        public void SeasonChanged_DoesNotFireOnNonBoundaryTicks()
        {
            var clock = new GameClock();
            int fires = 0;
            clock.SeasonChanged += _ => fires++;

            clock.Advance(27); // still within Spring
            Assert.Equal(0, fires);
        }
    }
}
