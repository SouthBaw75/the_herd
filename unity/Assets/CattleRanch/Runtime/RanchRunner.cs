using UnityEngine;
using CattleRanch.Sim; // RanchState, GameClock, Season  (see docs/ARCHITECTURE.md contract)

namespace CattleRanch.Game
{
    /// <summary>
    /// The single presentation-side driver for the day-atomic simulation.
    ///
    /// CONTRACT (design doc §6 / ARCHITECTURE.md non-negotiable #2):
    ///   * The atomic tick is ONE DAY. This class NEVER advances the sim from
    ///     Unity's per-frame Update(). Ticks are triggered explicitly by a
    ///     button press / keypress, so the sim stays deterministic and is never
    ///     frame-rate coupled.
    ///   * This MonoBehaviour OWNS NO GAME STATE. It holds *references* to the
    ///     authoritative sim objects (RanchState / GameClock) and only asks them
    ///     to advance. All mutation happens inside the sim assembly.
    ///
    /// NOTE FOR REVIEW: at authoring time GameClock/GameDate/Animal source EXISTS
    /// in the sim (verified) but RanchState does NOT yet. This file references
    /// both per the ARCHITECTURE.md contract and has NOT been compiled (no editor
    /// here). Items still unconfirmed are marked TODO(verify-in-editor) /
    /// TODO(verify-sim-api). The referenced sim is under parallel development, so
    /// re-check the API before relying on this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RanchRunner : MonoBehaviour
    {
        [Header("Bootstrap")]
        [Tooltip("Seed used to build the initial RanchState for this Phase 0 test run.")]
        [SerializeField] private long _seed = 1;

        [Header("Input (Phase 0 placeholder)")]
        [Tooltip("Key that advances the sim by exactly one day.")]
        [SerializeField] private KeyCode _advanceDayKey = KeyCode.Space;

        // --- References to the authoritative sim. This class does not own them. ---
        private RanchState _state;
        private GameClock _clock;

        /// <summary>Read-only access for view scripts (GameClockView, etc.).</summary>
        public RanchState State => _state;
        public GameClock Clock => _clock;

        /// <summary>Raised after a day has been advanced, so views can refresh.</summary>
        public event System.Action DayAdvanced;

        private void Awake()
        {
            // GameClock has a public ctor (GameClock(GameDate? start = null)),
            // verified against the sim source present at authoring time. We build
            // one here so the "advance time / date ticks" half of the Phase 0
            // exit test works immediately, without waiting on the rest of the sim.
            // The clock remains the authoritative owner of the date; we only
            // request ticks.
            if (_clock == null)
            {
                _clock = new GameClock(); // starts at Day 0 == Year 1, Spring, Day 1
            }

            // TODO(verify-sim-api): RanchState is documented in ARCHITECTURE.md as
            // the serializable root, but its construction/factory API (and, at
            // authoring time, its source) does not exist yet. Wire real bootstrap
            // here once the sim exposes it, e.g.:
            //
            //     _state = RanchState.NewGame(_seed);   // or a WorldFactory(seed)
            //
            // The sim is the single source of truth for the initial world; this
            // driver must never fabricate herd/paddock state itself. Until then
            // _state stays null; nothing in Phase 0's exit test depends on it.
        }

        /// <summary>
        /// Allows a test/bootstrap script to inject the authoritative sim objects
        /// instead of constructing them in Awake. Keeps this class read-only over
        /// state it does not own.
        /// </summary>
        public void Bind(RanchState state, GameClock clock)
        {
            _state = state;
            _clock = clock;
            DayAdvanced?.Invoke();
        }

        private void Update()
        {
            // The ONLY thing Update() does is poll for the manual advance input.
            // It NEVER ticks the sim on its own. (Uses legacy Input; Player
            // setting "Active Input Handling" must be "Both" or "Input Manager"
            // for this to fire — see ProjectSettings/README_SETTINGS.md.)
            if (Input.GetKeyDown(_advanceDayKey))
            {
                AdvanceDay();
            }
        }

        /// <summary>
        /// Advance the simulation by exactly one day. Safe to call from a UI
        /// Button's OnClick as well as the keypress above.
        /// </summary>
        public void AdvanceDay()
        {
            if (_clock == null)
            {
                Debug.LogWarning("[RanchRunner] AdvanceDay ignored — no GameClock bound yet.");
                return;
            }

            // Day-atomic tick. The clock is authoritative; we only request it.
            // (GameClock.Tick() verified against sim source at authoring time.)
            _clock.Tick();

            DayAdvanced?.Invoke();
        }

        /// <summary>Advance several days at once (still day-atomic, N discrete ticks).</summary>
        public void AdvanceDays(int days)
        {
            if (days < 0)
            {
                Debug.LogError("[RanchRunner] AdvanceDays called with negative days.");
                return;
            }
            for (int i = 0; i < days; i++)
            {
                AdvanceDay();
            }
        }
    }
}
