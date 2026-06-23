using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────
//  Enums
// ─────────────────────────────────────────────

/// <summary>Diet type — determined automatically by species.</summary>
public enum DietType
{
    Herbivore,  // herb
    Carnivore   // beast
}

/// <summary>Activity cycle — determined automatically by species.</summary>
public enum ActivityCycle
{
    Diurnal,    // 주행성
    Nocturnal   // 야행성
}

/// <summary>Season / content stage in which a species is unlocked.</summary>
public enum ContentStage
{
    S1,         // Squirrel, Roe Deer, Mouse, Rabbit
    S2,         // Sheep, Cat, Skunk, Wolf  (+S1)
    S3          // Tiger, Chicken, ...  (+S1+S2)
}

// ─────────────────────────────────────────────
//  Species-level static data
// ─────────────────────────────────────────────

/// <summary>
/// All static, species-wide traits for one animal type.
/// These values never change at runtime — treat this as read-only config data.
/// </summary>
[System.Serializable]
public class SpeciesData
{
    // ── Identity ──────────────────────────────
    [Tooltip("Internal species key used in code (matches ajong variable).")]
    public string speciesId;

    [Tooltip("Display name shown to the player (Korean).")]
    public string displayName;

    [Tooltip("Content stage this species is unlocked in.")]
    public ContentStage stage;

    // ── Biological traits ─────────────────────
    [Tooltip("Herbivore or Carnivore (atype). Set per-species, never changed at runtime.")]
    public DietType dietType;

    [Tooltip("Diurnal or Nocturnal (anight). Set per-species, never changed at runtime.")]
    public ActivityCycle activityCycle;

    // ── Nuisance probabilities (0–100) ────────
    [Tooltip("Probability (0–100) of causing floor nuisance / stomping (vnoise). " +
             "E.g. Squirrel = 0, Rabbit = 70.")]
    [Range(0, 100)]
    public int floorNuisanceProbability;

    [Tooltip("Probability (0–100) of causing wall nuisance (hnoise). " +
             "E.g. Squirrel = 0, Roe Deer = 50.")]
    [Range(0, 100)]
    public int wallNuisanceProbability;

    [Tooltip("Probability (0–100) of causing all-around nuisance (anoise). " +
             "E.g. Squirrel = 0, Wolf = 40.")]
    [Range(0, 100)]
    public int surroundNuisanceProbability;

    // ── Special room flag ─────────────────────
    [Tooltip("Whether this species requires a dedicated / special room (sroom). " +
             "Reserved for future design — leave false for now.")]
    public bool requiresSpecialRoom;

    // ── Special room effects ──────────────────
    [Tooltip("Does this species leave an odour in the room after checkout? (Skunk → smell = T)")]
    public bool leavesOdour;

    [Tooltip("Does this species risk damaging the room? (Mouse → broken = T)")]
    public bool causesDamage;
}

// ─────────────────────────────────────────────
//  Species database (ScriptableObject)
// ─────────────────────────────────────────────

/// <summary>
/// ScriptableObject that holds every species definition.
/// Create one asset via Assets → Create → AHP → Species Database.
/// </summary>
[CreateAssetMenu(menuName = "AHP/Species Database", fileName = "SpeciesDatabase")]
public class SpeciesDatabase : ScriptableObject
{
    public List<SpeciesData> allSpecies = new List<SpeciesData>();

    // Quick lookup by speciesId
    private Dictionary<string, SpeciesData> _lookup;

    private void OnEnable()
    {
        BuildLookup();
    }

    public void BuildLookup()
    {
        _lookup = new Dictionary<string, SpeciesData>();
        foreach (var s in allSpecies)
        {
            if (!string.IsNullOrEmpty(s.speciesId))
                _lookup[s.speciesId] = s;
        }
    }

    /// <summary>Returns the SpeciesData for the given id, or null if not found.</summary>
    public SpeciesData Get(string speciesId)
    {
        _lookup ??= new Dictionary<string, SpeciesData>();
        _lookup.TryGetValue(speciesId, out var data);
        return data;
    }
}