using UnityEngine;

// 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式
//  Runtime animal instance
// 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

/// <summary>
/// Represents one individual animal guest that visits the hotel.
/// Holds instance-level data (name, reservation flag) while pointing
/// to its species' shared <see cref="SpeciesData"/> for all static traits.
///
/// Usage:
///   var guest = new Animal(db.Get("rabbit"), "梯饜略檜", hasReservation: true);
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

    // 式式 Convenience pass-throughs 式式式式式式式式式式式式式
    //    These let other systems read traits without drilling into .species

    public string SpeciesId => species?.speciesId;
    public DietType DietType => species?.dietType ?? DietType.Herbivore;
    public ActivityCycle Activity => species?.activityCycle ?? ActivityCycle.Diurnal;
    public int FloorNoise => species?.floorNoiseProbability ?? 0;
    public int WallNoise => species?.wallNoiseProbability ?? 0;
    public int SurroundNoise => species?.surroundNoiseProbability ?? 0;
    public bool RequiresSpecialRoom => species?.requiresSpecialRoom ?? false;
    public bool LeavesOdour => species?.leavesOdour ?? false;
    public bool CausesDamage => species?.causesDamage ?? false;
    public bool IsCarnivore => DietType == DietType.Carnivore;
    public bool IsNocturnal => Activity == ActivityCycle.Nocturnal;

    // 式式 Constructor 式式式式式式式式式式式式式式式式式式式式式式式式式式式

    public Animal(SpeciesData speciesData, string name, bool hasReservation)
    {
        this.species = speciesData;
        this.guestName = name;
        this.hasReservation = hasReservation;
    }

    public override string ToString() =>
        $"[{species?.displayName ?? "?"}] {guestName} | " +
        $"蕨擒:{hasReservation} 衝撩:{DietType} �做�:{Activity} " +
        $"類除:{FloorNoise}% 漁除:{WallNoise}% 餌寞:{SurroundNoise}%";
}