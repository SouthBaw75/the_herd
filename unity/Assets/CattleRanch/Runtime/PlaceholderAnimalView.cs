using UnityEngine;
using CattleRanch.Sim; // Animal

namespace CattleRanch.Game
{
    /// <summary>
    /// A clickable placeholder for the Phase 0 exit test ("click a placeholder
    /// animal and advance time"). Put this on a cube/sprite GameObject that has a
    /// Collider (3D) or Collider2D so OnMouseDown fires.
    ///
    /// It READS an Animal from the sim (assigned by a spawner) and logs/selects it
    /// on click. It owns no game state — the Animal lives in RanchState.Herd.
    ///
    /// NOT COMPILED here. OnMouseDown requires legacy input to be active
    /// ("Active Input Handling" = Both or Input Manager) AND a collider on this
    /// object with a camera that can raycast it. See README_SETTINGS.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderAnimalView : MonoBehaviour
    {
        // The sim-side animal this placeholder represents. Reference only; the
        // authoritative object lives in RanchState.Herd.
        private Animal _animal;

        /// <summary>Fired when any placeholder animal is clicked (id-based selection).</summary>
        public static event System.Action<Animal> AnimalSelected;

        /// <summary>Assigned by a spawner when the placeholder is created.</summary>
        public void Bind(Animal animal)
        {
            _animal = animal;
        }

        // OnMouseDown is a Unity message. For a 2D sprite it needs a Collider2D
        // and a Physics2DRaycaster (or it works via the legacy per-object picking
        // when a Collider2D is present). TODO(verify-in-editor): confirm the
        // 2D collider + camera picking combination in the actual scene.
        private void OnMouseDown()
        {
            if (_animal == null)
            {
                Debug.Log($"[PlaceholderAnimalView] Clicked '{name}' — no Animal bound yet " +
                          "(Phase 0 placeholder). Wire one via Bind() once the sim spawns a herd.");
                return;
            }

            // Animal.Id / Animal.Name / Animal.Breed verified against sim source at authoring time.
            Debug.Log($"[PlaceholderAnimalView] Selected animal Id={_animal.Id} " +
                      $"Name='{_animal.Name}' Breed={_animal.Breed}");

            AnimalSelected?.Invoke(_animal);
        }
    }
}
