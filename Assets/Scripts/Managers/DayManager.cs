using AnimalHotel.Counter;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages one hotel day:
///   - Runs a fixed morning phase (MorningDuration seconds) then an afternoon phase (AfternoonDuration seconds)
///   - On morning start: checks out guests whose stay has ended, then generates new arrivals
///   - Day 1 morning: first guest is always a rabbit with a reservation (only in Stage 1)
///   - Holds the reservation list the UI and dialogue system read from
///   - Marks animals as arrived when they show up
///
/// Attach this to a DayManager GameObject in your main scene.
/// Assign the SpeciesDatabase asset in the Inspector.
/// </summary>
public class DayManager : MonoBehaviour
{
    public event System.Action OnTimeOfDayChanged;
    public event System.Action OnPhaseChanged;

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

    [Header("Checkout Flow")]
    [Tooltip("When enabled, the morning/afternoon timer does not decrease while checkout animations are playing.")]
    [SerializeField] private bool pauseClockDuringCheckout = true;

    [Tooltip("Base payment per night of stay.")]
    public float roomRatePerNight = 10f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>Which day we are currently on (starts at 1).</summary>
    public int CurrentDay { get; private set; } = 0;

    /// <summary>True while the morning phase is active; false during afternoon.</summary>
    public bool IsMorning { get; private set; } = true;

    /// <summary>Seconds remaining in the current phase.</summary>
    public float PhaseTimeRemaining { get; private set; }

    /// <summary>True while due guests are checking out one by one.</summary>
    public bool IsCheckoutInProgress { get; private set; }

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

    /// <summary>All guests scheduled to arrive today (both diurnal and nocturnal).</summary>
    public List<Animal> TodaysArrivals { get; private set; } = new List<Animal>();

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
        if (IsCheckoutInProgress && pauseClockDuringCheckout)
        {
            return;
        }

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
        if (IsCheckoutInProgress || PhaseTimeRemaining > 0f)
        {
            return;
        }

        if (counterFlow != null && counterFlow.IsBusy)
        {
            return;
        }

        if (IsMorning) StartAfternoon();
        else StartMorning();
    }

    /// <summary>
    /// Begins a new morning:
    ///   1. Increments the day counter.
    ///   2. Removes any guests whose CheckOutDay equals CurrentDay (they leave this morning).
    ///   3. Generates new arrivals and appends them to TodaysGuests.
    ///   4. On Day 1 the very first arrival is always a rabbit with a reservation (only in Stage 1).
    /// </summary>
    private void StartMorning()
    {
        if (AfternoonArrivals.Count > 0)
        {
            Debug.Log($"[DayManager] {AfternoonArrivals.Count} afternoon guest(s) never checked in — discarding.");
            AfternoonArrivals.Clear();
        }

        IsMorning = true;
        CurrentDay++;
        OnTimeOfDayChanged?.Invoke();
        PhaseTimeRemaining = morningDuration;
        int completedAdvancedCleanings = CompleteAdvancedCleaningRooms();
        if (roomManager != null) roomManager.ProcessNuisance();

        CheckedOutToday.Clear();
        List<Animal> departing = CollectDepartingGuests(nocturnal: false);

        // Generate arrivals now, but do not start their counter flow until checkout ends.
        MorningArrivals.Clear();
        AfternoonArrivals.Clear();
        TodaysArrivals.Clear();

        int departingRoomCount = roomManager != null
            ? departing.Count(guest => roomManager.GetRoomByOccupant(guest) != null)
            : 0;
        int nonOccupiedCount = roomManager != null
            ? Mathf.Min(RoomManager.RoomCount, roomManager.GetNonOccupiedRoomCount() + departingRoomCount)
            : 10;
        int maxReservations = Mathf.FloorToInt(nonOccupiedCount * 0.7f);

        if (unlockedStages == null)
        {
            Debug.LogWarning("[DayManager] unlockedStages was null! Initializing to default S1 stage list.");
            unlockedStages = new List<ContentStage> { ContentStage.S1 };
        }

        if (speciesDatabase == null)
        {
            Debug.LogError("[DayManager] speciesDatabase is null! Please assign a valid SpeciesDatabase in the Inspector.");
        }

        var newArrivals = AnimalFactory.CreateAnimals(
            speciesDatabase,
            unlockedStages,
            guestsPerDay,
            currentDay: CurrentDay,
            maxReservations: maxReservations,
            isFirstDay: CurrentDay == 1
        );

        TodaysArrivals.AddRange(newArrivals);

        foreach (var animal in newArrivals)
        {
            if (animal.IsNocturnal) AfternoonArrivals.Add(animal);
            else MorningArrivals.Add(animal);
        }

        Debug.Log($"[DayManager] Day {CurrentDay} morning started. " +
                  $"{departing.Count} morning checkout(s) queued, {MorningArrivals.Count} morning / " +
                  $"{AfternoonArrivals.Count} afternoon arrivals expected, " +
                  $"{TodaysGuests.Count} continuing guests in hotel, " +
                  $"{completedAdvancedCleanings} advanced cleaning(s) completed.");

        BeginCheckoutPhase(departing);
    }

    /// <summary>
    /// Transitions from morning to afternoon.
    /// Any diurnal guests still in MorningArrivals never checked in — discard them.
    /// </summary>
    private void StartAfternoon()
    {
        IsMorning = false;
        OnTimeOfDayChanged?.Invoke();
        PhaseTimeRemaining = afternoonDuration;
        int completedAdvancedCleanings = CompleteAdvancedCleaningRooms();
        if (roomManager != null) roomManager.ProcessNuisance();

        if (MorningArrivals.Count > 0)
        {
            Debug.Log($"[DayManager] {MorningArrivals.Count} morning guest(s) never checked in — discarding.");
            MorningArrivals.Clear();
        }

        List<Animal> departingNocturnal = CollectDepartingGuests(nocturnal: true);

        Debug.Log($"[DayManager] Day {CurrentDay} afternoon started. " +
                  $"{departingNocturnal.Count} nocturnal checkout(s) queued, " +
                  $"{AfternoonArrivals.Count} nocturnal guest(s) expected, " +
                  $"{completedAdvancedCleanings} advanced cleaning(s) completed.");

        BeginCheckoutPhase(departingNocturnal);
    }

    private int CompleteAdvancedCleaningRooms()
    {
        if (roomManager == null) return 0;
        return roomManager.CompleteAdvancedCleaningRooms();
    }

    private void NotifyPhaseChanged()
    {
        OnPhaseChanged?.Invoke();

        if (counterFlow != null)
        {
            counterFlow.OnPhaseChanged();
        }
    }

    private List<Animal> CollectDepartingGuests(bool nocturnal)
    {
        List<Animal> departing = TodaysGuests
            .Where(animal => animal.CheckOutDay == CurrentDay && animal.IsNocturnal == nocturnal)
            .ToList();

        // Fisher-Yates shuffle: guests leave in a different order each phase.
        for (int i = departing.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            Animal temp = departing[i];
            departing[i] = departing[swapIndex];
            departing[swapIndex] = temp;
        }

        return departing;
    }

    private void BeginCheckoutPhase(List<Animal> departingGuests)
    {
        if (departingGuests == null || departingGuests.Count == 0)
        {
            IsCheckoutInProgress = false;
            NotifyPhaseChanged();
            return;
        }

        IsCheckoutInProgress = true;
        StartCoroutine(RunCheckoutPhase(departingGuests));
    }

    private IEnumerator RunCheckoutPhase(List<Animal> departingGuests)
    {
        if (counterFlow != null)
        {
            yield return counterFlow.PlayCheckoutSequence(departingGuests, FinalizeCheckoutGuest);
        }
        else
        {
            Debug.LogWarning("[DayManager] CounterFlow is not assigned. Checkout will complete without animation.");
            foreach (Animal guest in departingGuests)
            {
                FinalizeCheckoutGuest(guest);
            }
        }

        IsCheckoutInProgress = false;
        Debug.Log($"[DayManager] Checkout sequence finished for {departingGuests.Count} guest(s). Arrivals can now begin.");
        NotifyPhaseChanged();
    }

    private void FinalizeCheckoutGuest(Animal guest)
    {
        if (guest == null || !TodaysGuests.Remove(guest))
        {
            return;
        }

        ArrivedGuests.Remove(guest);
        if (!CheckedOutToday.Contains(guest))
        {
            CheckedOutToday.Add(guest);
        }

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

        Debug.Log($"[DayManager] {guest.guestName} checked out. " +
                  $"Rated {rating}/10. Hotel total: {TotalMoney}, avg rating: {AverageRating:F1}");
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

    public void RecordRating(Animal guest, int rating, string reason = "")
    {
        rating = Mathf.Clamp(rating, 0, 10);
        _totalRatingSum += rating;
        _totalRatingCount++;
        AverageRating = (float)_totalRatingSum / _totalRatingCount;

        string guestName = guest != null ? guest.guestName : "Unknown guest";
        string reasonText = string.IsNullOrEmpty(reason) ? "" : $" Reason: {reason}.";
        Debug.Log($"[DayManager] {guestName} rated {rating}/10.{reasonText} Avg rating: {AverageRating:F1}");
    }

    public void SpendPhaseTime(float seconds, string reason = "")
    {
        if (seconds <= 0f) return;

        PhaseTimeRemaining = Mathf.Max(0f, PhaseTimeRemaining - seconds);
        string reasonText = string.IsNullOrEmpty(reason) ? "" : $" Reason: {reason}.";
        Debug.Log($"[DayManager] Spent {seconds:F1}s from the current phase.{reasonText} Remaining: {PhaseTimeRemaining:F1}s.");

        TryAdvancePhase();
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
