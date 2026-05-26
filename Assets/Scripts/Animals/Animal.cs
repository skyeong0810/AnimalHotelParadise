using UnityEngine;

// ─────────────────────────────────────────────
//  Runtime animal instance
// ─────────────────────────────────────────────

/// <summary>
/// Represents one individual animal guest that visits the hotel.
/// Holds instance-level data (name, reservation flag) while pointing
/// to its species' shared <see cref="SpeciesData"/> for all static traits.
///
/// Usage:
///   var guest = new Animal(db.Get("rabbit"), "김토깽이", hasReservation: true);
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

    // ── Convenience pass-throughs ─────────────
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

    // ── Constructor ───────────────────────────

    public Animal(SpeciesData speciesData, string name, bool hasReservation)
    {
        this.species = speciesData;
        this.guestName = name;
        this.hasReservation = hasReservation;
    }

    public override string ToString() =>
        $"[{species?.displayName ?? "?"}] {guestName} | " +
        $"예약:{hasReservation} 식성:{DietType} 활동:{Activity} " +
        $"층간:{FloorNoise}% 벽간:{WallNoise}% 사방:{SurroundNoise}%";
}