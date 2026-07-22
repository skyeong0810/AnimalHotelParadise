using System.Collections.Generic;
using UnityEngine;

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
        _lookup = new Dictionary<string, SpeciesData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var s in allSpecies)
        {
            if (!string.IsNullOrEmpty(s.speciesId))
                _lookup[s.speciesId] = s;
        }
    }

    /// <summary>Returns the SpeciesData for the given id, or null if not found.</summary>
    public SpeciesData Get(string speciesId)
    {
        _lookup ??= new Dictionary<string, SpeciesData>(System.StringComparer.OrdinalIgnoreCase);
        _lookup.TryGetValue(speciesId, out var data);
        return data;
    }
}
