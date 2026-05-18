using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages one hotel day:
///   - Generates today's guest list at day start
///   - Holds the reservation list the UI and dialogue system read from
///   - Marks animals as arrived when they show up
///
/// Attach this to a DayManager GameObject in your main scene.
/// Assign the SpeciesDatabase asset in the Inspector.
/// </summary>
public class DayManager : MonoBehaviour
{
    // ── Inspector fields ──────────────────────────────────────────────────────

    [Tooltip("The SpeciesDatabase ScriptableObject asset.")]
    public SpeciesDatabase speciesDatabase;

    [Tooltip("How many guests are generated at the start of each day.")]
    [Range(1, 20)]
    public int guestsPerDay = 10;

    [Tooltip("Which content stages are currently unlocked.")]
    public List<ContentStage> unlockedStages = new List<ContentStage> { ContentStage.S1 };

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>
    /// All animals generated for today — both reserved and walk-ins.
    /// This is the single source of truth the whole game reads from.
    /// </summary>
    public List<Animal> TodaysGuests { get; private set; } = new List<Animal>();

    /// <summary>Subset of TodaysGuests who have a reservation.</summary>
    public List<Animal> ReservationList => TodaysGuests.Where(a => a.hasReservation).ToList();

    /// <summary>Subset of TodaysGuests who have actually arrived so far today.</summary>
    public List<Animal> ArrivedGuests { get; private set; } = new List<Animal>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        StartNewDay();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this at the beginning of each new day.
    /// Clears yesterday's data and generates a fresh guest list.
    /// </summary>
    public void StartNewDay()
    {
        ArrivedGuests.Clear();

        TodaysGuests = AnimalFactory.CreateAnimals(speciesDatabase, unlockedStages, guestsPerDay);

        Debug.Log($"[DayManager] New day started. {TodaysGuests.Count} guests generated. " +
                  $"{ReservationList.Count} have reservations.");

        // Uncomment to print every guest to the console for testing:
        // foreach (var g in TodaysGuests) Debug.Log(g);
    }

    /// <summary>
    /// Call this when an animal physically arrives at the front desk.
    /// Looks them up from TodaysGuests and marks them as arrived.
    /// Returns the Animal so the dialogue system can use it immediately.
    /// </summary>
    /// <param name="guestName">The name of the arriving guest.</param>
    public Animal GuestArrived(string guestName)
    {
        var guest = TodaysGuests.FirstOrDefault(a => a.guestName == guestName);

        if (guest == null)
        {
            Debug.LogWarning($"[DayManager] '{guestName}' arrived but wasn't in today's guest list. Adding as walk-in.");
            // Walk-in not on list — still track them
            return null;
        }

        if (!ArrivedGuests.Contains(guest))
            ArrivedGuests.Add(guest);

        Debug.Log($"[DayManager] {guest.guestName} ({guest.species.displayName}) has arrived.");
        return guest;
    }

    /// <summary>
    /// Finds a guest by name from today's list.
    /// Used by the dialogue system to pull data for the conversation.
    /// </summary>
    public Animal GetGuest(string guestName)
    {
        return TodaysGuests.FirstOrDefault(a => a.guestName == guestName);
    }
}
