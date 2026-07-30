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
    /// Whether this guest has already called the front desk regarding nuisance during their stay.
    /// </summary>
    public bool hasCalledNuisance;

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
