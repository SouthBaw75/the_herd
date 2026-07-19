using UnityEngine;
using TMPro; // com.unity.ugui in Unity 6 ships TextMeshPro; asmdef refs "Unity.TextMeshPro"
using CattleRanch.Sim; // GameDate, Season

namespace CattleRanch.Game
{
    /// <summary>
    /// Read-only view: renders the sim's current date/season into a TMP label.
    /// Owns no state — it only READS GameClock.Date each time the day changes.
    ///
    /// Reading in a view is presentation, not simulation, so it is fine that this
    /// runs outside the day-tick. It refreshes on RanchRunner.DayAdvanced (event)
    /// and once on enable, rather than reformatting every frame.
    ///
    /// NOT COMPILED (no Unity editor here). TMP + sim API usage flagged below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClockView : MonoBehaviour
    {
        [Tooltip("The driver whose clock we display.")]
        [SerializeField] private RanchRunner _runner;

        [Tooltip("TMP label to write the date into. Assign a TextMeshProUGUI in the scene.")]
        [SerializeField] private TMP_Text _label; // TODO(verify-in-editor): TMP_Text base type

        private void OnEnable()
        {
            if (_runner != null)
            {
                _runner.DayAdvanced += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_runner != null)
            {
                _runner.DayAdvanced -= Refresh;
            }
        }

        private void Refresh()
        {
            if (_label == null || _runner == null || _runner.Clock == null)
            {
                return; // Nothing bound yet (e.g. before sim bootstrap exists).
            }

            // Property names verified against sim source at authoring time:
            //   GameClock.Date -> GameDate { Year, Season, DayOfSeason, TotalDays }
            GameDate date = _runner.Clock.Date;
            _label.text = $"Year {date.Year} — {date.Season} — Day {date.DayOfSeason}";
        }
    }
}
