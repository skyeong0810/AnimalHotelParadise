using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages one hotel day:
///   - Runs a fixed morning phase (MorningDuration seconds) then an afternoon phase (AfternoonDuration seconds)
///   - On morning start: generates a fresh guest list (discarding the previous day's)
///   - Day 1 morning: first guest is always a rabbit with a reservation
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

    [Tooltip("Length of the morning phase in seconds.")]
    public float morningDuration = 5f;

    [Tooltip("Length of the afternoon phase in seconds.")]
    public float afternoonDuration = 5f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>Which day we are currently on (starts at 1).</summary>
    public int CurrentDay { get; private set; } = 0;

    /// <summary>True while the morning phase is active; false during afternoon.</summary>
    public bool IsMorning { get; private set; } = true;

    /// <summary>Seconds remaining in the current phase (0–morningDuration or 0–afternoonDuration).</summary>
    public float PhaseTimeRemaining { get; private set; }

    /// <summary>
    /// All animals generated for today — both reserved and walk-ins.
    /// This is the single source of truth the whole game reads from.
    /// Replaced entirely at the start of each morning; previous day's list is discarded.
    /// </summary>
    public List<Animal> TodaysGuests { get; private set; } = new List<Animal>();

    /// <summary>Subset of TodaysGuests who have a reservation.</summary>
    public List<Animal> ReservationList => TodaysGuests.Where(a => a.hasReservation).ToList();

    /// <summary>Subset of TodaysGuests who have actually arrived so far today.</summary>
    public List<Animal> ArrivedGuests { get; private set; } = new List<Animal>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        StartMorning();
    }

    private void Update()
    {
        PhaseTimeRemaining -= Time.deltaTime;

        if (PhaseTimeRemaining <= 0f)
        {
            if (IsMorning)
                StartAfternoon();
            else
                StartMorning();
        }
    }

    // ── Phase transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Begins a new morning: increments the day counter, discards yesterday's guest list,
    /// generates a fresh one, and resets the phase timer.
    /// On Day 1 the very first guest is always a rabbit with a reservation.
    /// </summary>
    private void StartMorning()
    {
        IsMorning = true;
        CurrentDay++;
        PhaseTimeRemaining = morningDuration;

        ArrivedGuests.Clear();

        TodaysGuests = AnimalFactory.CreateAnimals(
            speciesDatabase,
            unlockedStages,
            guestsPerDay,
            isFirstDay: CurrentDay == 1
        );

        Debug.Log($"[DayManager] Day {CurrentDay} morning started. " +
                  $"{TodaysGuests.Count} guests generated, " +
                  $"{ReservationList.Count} with reservations.");
    }

    /// <summary>Transitions from morning to afternoon and resets the phase timer.</summary>
    private void StartAfternoon()
    {
        IsMorning = false;
        PhaseTimeRemaining = afternoonDuration;

        Debug.Log($"[DayManager] Day {CurrentDay} afternoon started.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

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
            Debug.LogWarning($"[DayManager] '{guestName}' arrived but wasn't in today's guest list.");
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