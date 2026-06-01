using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates random Animal instances.
/// Pure static utility — no scene object needed, no MonoBehaviour.
/// Call AnimalFactory.CreateAnimals() from DayManager.
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
    /// Probability (0–1) that a randomly generated animal has a reservation.
    /// </summary>
    private const float ReservationChance = 0.6f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates one random Animal from the unlocked species in the database.
    /// </summary>
    public static Animal CreateAnimal(SpeciesDatabase database, List<ContentStage> unlockedStages)
    {
        var pool = database.allSpecies
            .Where(s => unlockedStages.Contains(s.stage))
            .ToList();

        if (pool.Count == 0)
        {
            Debug.LogError("AnimalFactory: No unlocked species found. Check your SpeciesDatabase and unlockedStages.");
            return null;
        }

        SpeciesData species = pool[Random.Range(0, pool.Count)];

        string lastName = LastNames[Random.Range(0, LastNames.Length)];
        string firstName = FirstNames[Random.Range(0, FirstNames.Length)];
        string fullName = lastName + firstName;

        bool hasReservation = Random.value < ReservationChance;

        return new Animal(species, fullName, hasReservation);
    }

    /// <summary>
    /// Creates a batch of random animals in one call.
    /// </summary>
    /// <param name="count">How many animals to generate.</param>
    /// <param name="isFirstDay">
    /// When true, the very first guest in the returned list is always a rabbit
    /// with a reservation, and the remaining (count - 1) slots are filled randomly.
    /// </param>
    public static List<Animal> CreateAnimals(
        SpeciesDatabase database,
        List<ContentStage> unlockedStages,
        int count,
        bool isFirstDay = false)
    {
        var result = new List<Animal>();

        if (isFirstDay)
        {
            // Guaranteed first guest: a rabbit with a reservation.
            SpeciesData rabbitData = database.Get("rabbit");
            if (rabbitData == null)
                Debug.LogError("AnimalFactory: 'rabbit' not found in SpeciesDatabase. Check speciesId.");
            else
            {
                string lastName = LastNames[Random.Range(0, LastNames.Length)];
                string firstName = FirstNames[Random.Range(0, FirstNames.Length)];
                result.Add(new Animal(rabbitData, lastName + firstName, hasReservation: true));
            }
        }

        int remaining = count - result.Count;
        for (int i = 0; i < remaining; i++)
        {
            var animal = CreateAnimal(database, unlockedStages);
            if (animal != null)
                result.Add(animal);
        }

        foreach (var animal in result)
            Debug.Log(animal.ToString());

        return result;
    }
}