using UnityEngine;

// 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
//  Runtime animal instance
// 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

/// <summary>
/// Represents one individual animal guest that visits the hotel.
/// Holds instance-level data (name, reservation, stay duration) while pointing
/// to its species' shared <see cref="SpeciesData"/> for all static traits.
///
/// Usage:
///   var guest = new Animal(db.Get("rabbit"), "梯饜略檜", hasReservation: true, checkInDay: 1, stayNights: 2);
/// </summary>
[System.Serializable]
public class Animal
{
    // 式式 Instance identity 式式式式式式式式式式式式式式式式式式式式式

    /// <summary>Randomly generated guest name (aname). E.g. "梯饜略檜".</summary>
    public string guestName;

    /// <summary>Whether this animal is on today's reservation list (rbook).</summary>
    public bool hasReservation;

    /// <summary>Reference to the shared, species-wide data.</summary>
    public SpeciesData species;

    // 式式 Stay duration 式式式式式式式式式式式式式式式式式式式式式式式式式

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

    // 式式 Convenience pass-throughs 式式式式式式式式式式式式式
    //    These let other systems read traits without drilling into .species

    public string SpeciesId => species?.speciesId;
    public DietType DietType => species?.dietType ?? DietType.Herbivore;
    public ActivityCycle Activity => species?.activityCycle ?? ActivityCycle.Diurnal;
    public int FloorNuisance => species?.floorNuisanceProbability ?? 0;
    public int WallNuisance => species?.wallNuisanceProbability ?? 0;
    public int SurroundNuisance => species?.surroundNuisanceProbability ?? 0;
    public bool RequiresSpecialRoom => species?.requiresSpecialRoom ?? false;
    public bool LeavesOdour => species?.leavesOdour ?? false;
    public bool CausesDamage => species?.causesDamage ?? false;
    public bool IsCarnivore => DietType == DietType.Carnivore;
    public bool IsNocturnal => Activity == ActivityCycle.Nocturnal;

    // 式式 Constructor 式式式式式式式式式式式式式式式式式式式式式式式式式式式

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
        $"蕨擒:{hasReservation} 衝撩:{DietType} �做�:{Activity} " +
        $"類除:{FloorNuisance}% 漁除:{WallNuisance}% 餌寞:{SurroundNuisance}% " +
        $"羹觼檣:Day {checkInDay} 羹觼嬴醒:Day {CheckOutDay}";
}