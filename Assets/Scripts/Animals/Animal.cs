using UnityEngine;

// ─────────────────────────────────────────────
//  Runtime animal instance
// ─────────────────────────────────────────────

/// <summary>
/// Represents one individual animal guest that visits the hotel.
/// Holds instance-level data (name, reservation, stay duration) while pointing
/// to its species' shared <see cref="SpeciesData"/> for all static traits.
///
/// Usage:
///   var guest = new Animal(db.Get("rabbit"), "김토깽이", hasReservation: true, checkInDay: 1, stayNights: 2);
/// </summary>
[System.Serializable]
public class Animal
{
    // ── Instance identity ─────────────────────

    /// <summary>Randomly generated guest name (aname). E.g. "김토깽이".</summary>
    public string guestName;

    /// <summary>Whether this animal is on today's reservation list (rbook).</summary>
    public bool hasReservation;

    /// <summary>Reference to the shared, species-wide data.</summary>
    public SpeciesData species;

    // ── Stay duration ─────────────────────────

    /// <summary>
    /// The day number this guest checks in.
    /// Set by AnimalFactory at generation time; equals CurrentDay at the time of creation.
    /// </summary>
    public int checkInDay;

    /// <summary>
    /// How many nights the guest will stay (minimum 1).
    /// Randomly assigned by AnimalFactory.
    /// </summary>
    public int stayNights;

    /// <summary>
    /// The day number on which this guest should check out.
    /// A guest who checks in on day 3 and stays 2 nights checks out on day 5.
    /// DayManager compares CurrentDay against this at the start of each morning.
    /// </summary>
    public int CheckOutDay => checkInDay + stayNights;

    // ── Convenience pass-throughs ─────────────
    //    These let other systems read traits without drilling into .species

    public string SpeciesId => species?.speciesId;
    public Sprite ReservationIconSprite => species != null && species.reservationIconSprite != null
        ? species.reservationIconSprite
        : species?.speciesSprite;
    public float CounterSpriteScaleMultiplier => species != null ? Mathf.Max(0.1f, species.counterSpriteScaleMultiplier) : 1f;
    public DietType DietType => species?.dietType ?? DietType.Herbivore;
    public ActivityCycle Activity => species?.activityCycle ?? ActivityCycle.Diurnal;
    public int FloorNuisance => species?.floorNuisanceProbability ?? 0;
    public int WallNuisance => species?.wallNuisanceProbability ?? 0;
    public int SurroundNuisance => species?.surroundNuisanceProbability ?? 0;
    public bool RequiresSpecialRoom => species?.requiresSpecialRoom ?? false;
    public bool LeavesOdour => species?.leavesOdour ?? false;
    public bool CausesDamage => species?.causesDamage ?? false;
    public int DamageProbability => species?.damageProbability ?? 0;
    public bool IsCarnivore => DietType == DietType.Carnivore;
    public bool IsNocturnal => Activity == ActivityCycle.Nocturnal;

    // ── Instance Nuisance Determination ───────

    /// <summary>
    /// Whether nuisance probabilities have been evaluated for this guest instance.
    /// Once determined, these flags persist until the guest checks out.
    /// </summary>
    public bool hasDeterminedNuisance;
    public bool willCauseFloorNuisance;
    public bool willCauseWallNuisance;
    public bool willCauseSurroundNuisance;

    /// <summary>
    /// Whether this guest currently has an unanswered-for nuisance complaint outstanding. Set true the
    /// moment their call starts ringing; blocks any further call from being scheduled while true (see
    /// RoomManager.ScheduleNuisanceCall). Reset back to false once a promised room move actually
    /// happens (see RoomManager.MoveAnimal) — so the guest can complain again if the *new* room turns
    /// out to have its own nuisance problem. It is NOT reset when a complaint goes unresolved (no room
    /// offered, or the call was missed) — an unresolved guest never gets a second chance to call.
    /// </summary>
    public bool hasCalledNuisance;

    /// <summary>
    /// How many times this guest has actually placed a nuisance complaint call during their stay
    /// (i.e., how many times their call has started ringing). Unlike <see cref="hasCalledNuisance"/>,
    /// this never resets — DayManager.GetCheckoutRating uses it to tell "resolved cleanly on the first
    /// complaint" apart from "resolved, but the nuisance kept recurring after being moved" (the
    /// "부분 해결" rating tier).
    /// </summary>
    public int nuisanceComplaintCount;

    /// <summary>How a nuisance complaint (if any) for this guest was ultimately handled.</summary>
    public enum NuisanceResolution
    {
        /// <summary>This guest never had to call about a nuisance.</summary>
        None,
        /// <summary>Staff answered and moved the guest to a new room.</summary>
        Resolved,
        /// <summary>The call was missed, or staff answered but had no room to offer.</summary>
        Unresolved
    }

    /// <summary>
    /// Outcome of this guest's nuisance complaint, if they had one. Read at checkout time to
    /// determine the rating penalty (see DayManager.FinalizeCheckoutGuest).
    /// </summary>
    public NuisanceResolution nuisanceResolution = NuisanceResolution.None;

    /// <summary>
    /// Evaluates nuisance probabilities once per guest instance and saves the results.
    /// </summary>
    public void DetermineNuisance()
    {
        if (hasDeterminedNuisance) return;

        willCauseFloorNuisance = FloorNuisance > 0 && Random.Range(0, 100) < FloorNuisance;
        willCauseWallNuisance = WallNuisance > 0 && Random.Range(0, 100) < WallNuisance;
        willCauseSurroundNuisance = SurroundNuisance > 0 && Random.Range(0, 100) < SurroundNuisance;
        hasDeterminedNuisance = true;
    }

    // ── Constructor ───────────────────────────

    public Animal(SpeciesData speciesData, string name, bool hasReservation, int checkInDay, int stayNights)
    {
        this.species = speciesData;
        this.guestName = name;
        this.hasReservation = hasReservation;
        this.checkInDay = checkInDay;
        this.stayNights = stayNights;
    }

    public override string ToString() =>
        $"[{species?.displayName ?? "?"}] {guestName} | " +
        $"예약:{hasReservation} 식성:{DietType} 활동:{Activity} " +
        $"층간:{FloorNuisance}% 벽간:{WallNuisance}% 사방:{SurroundNuisance}% " +
        $"체크인:Day {checkInDay} 체크아웃:Day {CheckOutDay}";
}
