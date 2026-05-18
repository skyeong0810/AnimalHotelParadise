using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates random Animal instances.
/// Pure static utility — no scene object needed, no MonoBehaviour.
/// Call AnimalFactory.CreateAnimal() from anywhere.
/// </summary>
public static class AnimalFactory
{
    // ── Name pools ────────────────────────────────────────────────────────────

    private static readonly string[] LastNames =
    {
        "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임"
    };

    private static readonly string[] FirstNames =
    {
        // Animal-flavored word clusters (mix freely — doesn't have to match species)
        "토깽이", "멍이", "꼬리", "발바닥", "솜털", "콧수염", "귀순이",
        "뽀송이", "야옹이", "어흥이", "달려라", "깡총이", "꾸벅이", "뾰족이",
        "털보", "눈망울", "꼬물이", "앞발", "냥이", "바둑이"
        // Add more as needed
    };

    // ── Reservation probability ───────────────────────────────────────────────

    /// <summary>
    /// Probability (0–1) that a generated animal has a reservation.
    /// Tweak this or pass it in per day if you want dynamic rates.
    /// </summary>
    private const float ReservationChance = 0.6f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates one random Animal from the unlocked species in the database.
    /// </summary>
    /// <param name="database">The SpeciesDatabase ScriptableObject asset.</param>
    /// <param name="unlockedStages">Which content stages are currently unlocked.</param>
    /// <returns>A fully populated Animal instance.</returns>
    public static Animal CreateAnimal(SpeciesDatabase database, List<ContentStage> unlockedStages)
    {
        // 1. Filter to unlocked species only
        var pool = database.allSpecies
            .Where(s => unlockedStages.Contains(s.stage))
            .ToList();

        if (pool.Count == 0)
        {
            Debug.LogError("AnimalFactory: No unlocked species found. Check your SpeciesDatabase and unlockedStages.");
            return null;
        }

        // 2. Pick a random species
        SpeciesData species = pool[Random.Range(0, pool.Count)];

        // 3. Build a random name
        string lastName  = LastNames[Random.Range(0, LastNames.Length)];
        string firstName = FirstNames[Random.Range(0, FirstNames.Length)];
        string fullName  = lastName + firstName;

        // 4. Decide reservation status
        bool hasReservation = Random.value < ReservationChance;

        // 5. Construct and return
        return new Animal(species, fullName, hasReservation);
    }

    /// <summary>
    /// Creates a batch of random animals in one call.
    /// </summary>
    /// <param name="count">How many animals to generate.</param>
    public static List<Animal> CreateAnimals(SpeciesDatabase database, List<ContentStage> unlockedStages, int count)
    {
        var result = new List<Animal>();
        for (int i = 0; i < count; i++)
        {
            var animal = CreateAnimal(database, unlockedStages);
            if (animal != null)
                result.Add(animal);
        }
        foreach (var animal in result) Debug.Log(animal.ToString());
        return result;
    }
}
