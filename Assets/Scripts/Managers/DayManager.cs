using AnimalHotel.Counter;
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

    [Tooltip("Reference to the CounterFlow in the scene.")]
    public CounterFlow counterFlow;

    [Tooltip("Reference to the RoomManager in the scene.")]
    public RoomManager roomManager;

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

    /// <summary>
    /// Diurnal guests scheduled to arrive this morning.
    /// Populated at the start of each morning; cleared when the morning phase ends.
    /// </summary>
    public List<Animal> MorningArrivals { get; private set; } = new List<Animal>();

    /// <summary>
    /// Nocturnal guests scheduled to arrive this afternoon.
    /// Populated at the start of each morning; cleared when the afternoon phase ends.
    /// </summary>
    public List<Animal> AfternoonArrivals { get; private set; } = new List<Animal>();

    /// <summary>Subset of TodaysGuests who have actually checked in so far today.</summary>
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
        if (PhaseTimeRemaining > 0f)
        {
            PhaseTimeRemaining -= Time.deltaTime;
        }
        else
        {
            TryAdvancePhase();
        }
    }

    // ── Phase transitions ─────────────────────────────────────────────────────
    public void TryAdvancePhase()
    {
        if (PhaseTimeRemaining <= 0f)
        {
            if (counterFlow != null && counterFlow.GetCurrentGuest() != null)
            {
                return;
            }

            if (IsMorning) StartAfternoon();
            else StartMorning();
        }
    }

    /// <summary>
    /// Begins a new morning:
    ///   1. Increments the day counter.
    ///   2. Removes any guests whose CheckOutDay equals CurrentDay (they leave this morning).
    ///   3. Generates new arrivals and appends them to TodaysGuests.
    ///   4. On Day 1 the very first arrival is always a rabbit with a reservation.
    /// </summary>
    private void StartMorning()
    {
        // Purge any nocturnal guests from yesterday's afternoon who never checked in.
        if (AfternoonArrivals.Count > 0)
        {
            Debug.Log($"[DayManager] {AfternoonArrivals.Count} afternoon guest(s) never checked in — discarding.");
            AfternoonArrivals.Clear();
        }

        IsMorning = true;
        CurrentDay++;
        PhaseTimeRemaining = morningDuration;

        // 1. Check out guests whose stay has ended.
        CheckedOutToday.Clear();
        var departing = CheckOutDepartingGuests(nocturnal: false);

        // 2. Generate new arrivals and sort them into morning / afternoon queues.
        MorningArrivals.Clear();
        AfternoonArrivals.Clear();

        var newArrivals = AnimalFactory.CreateAnimals(
            speciesDatabase,
            unlockedStages,
            guestsPerDay,
            currentDay: CurrentDay,
            isFirstDay: CurrentDay == 1
        );

        foreach (var a in newArrivals)
        {
            if (a.IsNocturnal) AfternoonArrivals.Add(a);
            else MorningArrivals.Add(a);
        }

        // Note: new arrivals are NOT added to TodaysGuests yet — they join only after
        // checking in at the counter (via CheckIn).

        Debug.Log($"[DayManager] Day {CurrentDay} morning started. " +
                  $"{departing.Count} checked out, {MorningArrivals.Count} morning / " +
                  $"{AfternoonArrivals.Count} afternoon arrivals expected, " +
                  $"{TodaysGuests.Count} continuing guests in hotel.");

        if (counterFlow != null)
        {
            counterFlow.OnPhaseChanged();
        }
    }

    /// <summary>
    /// Transitions from morning to afternoon.
    /// Any diurnal guests still in MorningArrivals never checked in — discard them.
    /// </summary>
    private void StartAfternoon()
    {
        IsMorning = false;
        PhaseTimeRemaining = afternoonDuration;

        if (MorningArrivals.Count > 0)
        {
            Debug.Log($"[DayManager] {MorningArrivals.Count} morning guest(s) never checked in — discarding.");
            MorningArrivals.Clear();
        }

        // Check out nocturnal guests whose stay has ended.
        var departingNocturnal = CheckOutDepartingGuests(nocturnal: true);

        Debug.Log($"[DayManager] Day {CurrentDay} afternoon started. " +
                  $"{departingNocturnal.Count} nocturnal guest(s) checked out, " +
                  $"{AfternoonArrivals.Count} nocturnal guest(s) expected.");

        if (counterFlow != null)
        {
            counterFlow.OnPhaseChanged();
        }
    }

    private List<Animal> CheckOutDepartingGuests(bool nocturnal)
    {
        var departing = TodaysGuests.Where(a => a.CheckOutDay == CurrentDay && a.IsNocturnal == nocturnal).ToList();
        foreach (var guest in departing)
        {
            TodaysGuests.Remove(guest);
            ArrivedGuests.Remove(guest);
            CheckedOutToday.Add(guest);
            int rating = Random.Range(0, 11); // 0–10 inclusive

            _totalRatingSum += rating;
            _totalRatingCount++;
            AverageRating = (float)_totalRatingSum / _totalRatingCount;

            if (roomManager != null)
            {
                var room = roomManager.GetRoomByOccupant(guest);
                if (room != null) roomManager.VacateRoom(room.roomNumber);
                else Debug.LogWarning($"[DayManager] No room found for {guest.guestName} on checkout.");
            }

            Debug.Log($"[DayManager] {guest.guestName} checked out (nocturnal: {nocturnal}). " +
                      $"Rated {rating}/10. " +
                      $"Hotel total: {TotalMoney}, avg rating: {AverageRating:F1}");
        }
        return departing;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this when an animal walks up to the front desk counter.
    /// Finds the guest in the appropriate arrival queue and returns them
    /// so the dialogue system can start a conversation.
    /// Does NOT add them to TodaysGuests or charge payment yet — call CheckIn() for that.
    /// </summary>
    public Animal GuestArrived(string guestName)
    {
        // Search whichever queue is active for the current phase.
        var queue = IsMorning ? MorningArrivals : AfternoonArrivals;
        var guest = queue.FirstOrDefault(a => a.guestName == guestName);

        if (guest == null)
        {
            Debug.LogWarning($"[DayManager] '{guestName}' not found in the current arrival queue.");
            return null;
        }
        return guest;
    }

    /// <summary>
    /// Call this after the player completes check-in dialogue and confirms the guest's stay.
    /// Removes the guest from the arrival queue, adds them to TodaysGuests and ArrivedGuests,
    /// and collects payment.
    /// </summary>
    public void CheckIn(Animal guest)
    {
        if (guest == null) return;

        // Remove from whichever queue they came from.
        bool removed = MorningArrivals.Remove(guest) || AfternoonArrivals.Remove(guest);
        if (!removed)
            Debug.LogWarning($"[DayManager] CheckIn called for {guest.guestName} but they weren't in any arrival queue.");

        if (!TodaysGuests.Contains(guest))
            TodaysGuests.Add(guest);

        if (!ArrivedGuests.Contains(guest))
            ArrivedGuests.Add(guest);

        float payment = guest.stayNights * roomRatePerNight;
        TotalMoney += payment;

        Debug.Log($"[DayManager] {guest.guestName} checked in. " +
                  $"Paid {payment} ({guest.stayNights} night(s)). " +
                  $"Staying until Day {guest.CheckOutDay}. " +
                  $"Hotel total: {TotalMoney}");
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