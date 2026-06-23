using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages one hotel day:
///   - Runs a fixed morning phase (MorningDuration seconds) then an afternoon phase (AfternoonDuration seconds)
///   - On morning start: checks out guests whose stay has ended, then generates new arrivals
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

    [Tooltip("How many new guests are generated at the start of each morning.")]
    [Range(1, 20)]
    public int guestsPerDay = 10;

    [Tooltip("Which content stages are currently unlocked.")]
    public List<ContentStage> unlockedStages = new List<ContentStage> { ContentStage.S1 };

    [Tooltip("Length of the morning phase in seconds.")]
    public float morningDuration = 5f;

    [Tooltip("Length of the afternoon phase in seconds.")]
    public float afternoonDuration = 5f;

    [Tooltip("Base payment per night of stay.")]
    public float roomRatePerNight = 10f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>Which day we are currently on (starts at 1).</summary>
    public int CurrentDay { get; private set; } = 0;

    /// <summary>True while the morning phase is active; false during afternoon.</summary>
    public bool IsMorning { get; private set; } = true;

    /// <summary>Seconds remaining in the current phase.</summary>
    public float PhaseTimeRemaining { get; private set; }

    public float TotalMoney { get; private set; } = 0f;
    public float AverageRating { get; private set; } = 5.0f;

    private int _totalRatingSum = 0;
    private int _totalRatingCount = 0;

    /// <summary>
    /// All guests currently in the hotel — both staying over from previous days
    /// and new arrivals generated this morning.
    /// New arrivals are appended each morning; checked-out guests are removed.
    /// </summary>
    public List<Animal> TodaysGuests { get; private set; } = new List<Animal>();

    /// <summary>Subset of TodaysGuests who have a reservation.</summary>
    public List<Animal> ReservationList => TodaysGuests.Where(a => a.hasReservation).ToList();

    /// <summary>Subset of TodaysGuests who have actually arrived so far today.</summary>
    public List<Animal> ArrivedGuests { get; private set; } = new List<Animal>();

    /// <summary>Guests who checked out at the start of this morning. Cleared each morning.</summary>
    public List<Animal> CheckedOutToday { get; private set; } = new List<Animal>();

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
    /// Begins a new morning:
    ///   1. Increments the day counter.
    ///   2. Removes any guests whose CheckOutDay equals CurrentDay (they leave this morning).
    ///   3. Generates new arrivals and appends them to TodaysGuests.
    ///   4. On Day 1 the very first arrival is always a rabbit with a reservation.
    /// </summary>
    private void StartMorning()
    {
        IsMorning = true;
        CurrentDay++;
        PhaseTimeRemaining = morningDuration;

        // 1. Check out guests whose stay has ended.
        CheckedOutToday.Clear();
        var departing = TodaysGuests.Where(a => a.CheckOutDay == CurrentDay).ToList();
        foreach (var guest in departing)
        {
            TodaysGuests.Remove(guest);
            ArrivedGuests.Remove(guest);
            CheckedOutToday.Add(guest);
            int rating = Random.Range(0, 11); // 0–10 inclusive

            _totalRatingSum += rating;
            _totalRatingCount++;
            AverageRating = (float)_totalRatingSum / _totalRatingCount;

            Debug.Log($"[DayManager] {guest.guestName} checked out. " +
                      $"Rated {rating}/10. " +
                      $"Hotel total: {TotalMoney}, avg rating: {AverageRating:F1}");
        }

        // 2. Generate new arrivals for this morning.
        var newArrivals = AnimalFactory.CreateAnimals(
            speciesDatabase,
            unlockedStages,
            guestsPerDay,
            currentDay: CurrentDay,
            isFirstDay: CurrentDay == 1
        );
        TodaysGuests.AddRange(newArrivals);

        Debug.Log($"[DayManager] Day {CurrentDay} morning started. " +
                  $"{departing.Count} checked out, {newArrivals.Count} new arrivals, " +
                  $"{TodaysGuests.Count} total guests in hotel.");
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
    public Animal GuestArrived(string guestName)
    {
        var guest = TodaysGuests.FirstOrDefault(a => a.guestName == guestName);

        if (guest == null)
        {
            Debug.LogWarning($"[DayManager] '{guestName}' arrived but wasn't in today's guest list.");
            return null;
        }

        if (!ArrivedGuests.Contains(guest))
        {
            ArrivedGuests.Add(guest);

            float payment = guest.stayNights * roomRatePerNight;
            TotalMoney += payment;

            Debug.Log($"[DayManager] {guest.guestName} paid {payment} on arrival.");
        }


        Debug.Log($"[DayManager] {guest.guestName} ({guest.species.displayName}) has arrived. " +
                  $"Staying until Day {guest.CheckOutDay}.");
        return guest;
    }

    /// <summary>
    /// Finds a guest by name from the current hotel guest list.
    /// Used by the dialogue system to pull data for the conversation.
    /// </summary>
    public Animal GetGuest(string guestName)
    {
        return TodaysGuests.FirstOrDefault(a => a.guestName == guestName);
    }
}