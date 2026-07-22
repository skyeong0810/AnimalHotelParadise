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

    // ── Stay duration range ───────────────────────────────────────────────────

    /// <summary>Minimum number of nights a guest may stay.</summary>
    private const int MinNights = 1;

    /// <summary>Maximum number of nights a guest may stay.</summary>
    private const int MaxNights = 3;

    // ── Reservation probability ───────────────────────────────────────────────

    /// <summary>Probability (0–1) that a randomly generated animal has a reservation.</summary>
    private const float ReservationChance = 0.6f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates one random Animal from the unlocked species in the database.
    /// </summary>
    /// <param name="currentDay">The day number this animal is being generated for (used as checkInDay).</param>
    public static Animal CreateAnimal(SpeciesDatabase database, List<ContentStage> unlockedStages, int currentDay, bool forceNoReservation = false)
    {
        if (database == null || database.allSpecies == null)
        {
            Debug.LogError("AnimalFactory: SpeciesDatabase or its allSpecies list is null.");
            return null;
        }

        if (unlockedStages == null || unlockedStages.Count == 0)
        {
            Debug.LogError("AnimalFactory: unlockedStages is null or empty.");
            return null;
        }

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

        bool hasReservation = !forceNoReservation && (Random.value < ReservationChance);
        int stayNights = Random.Range(MinNights, MaxNights + 1);

        return new Animal(species, fullName, hasReservation, checkInDay: currentDay, stayNights: stayNights);
    }

    /// <summary>
    /// Creates a batch of random animals in one call, limiting the number of reservation guests.
    /// </summary>
    /// <param name="count">How many animals to generate.</param>
    /// <param name="currentDay">The day number guests are being generated for.</param>
    /// <param name="maxReservations">Maximum number of animals in the batch allowed to have a reservation.</param>
    /// <param name="isFirstDay">
    /// When true and in stage 1, the very first guest in the returned list is always a rabbit
    /// with a reservation. The remaining slots are filled randomly.
    /// </param>
    public static List<Animal> CreateAnimals(
        SpeciesDatabase database,
        List<ContentStage> unlockedStages,
        int count,
        int currentDay,
        int maxReservations,
        bool isFirstDay = false)
    {
        var result = new List<Animal>();

        if (database == null)
        {
            Debug.LogError("AnimalFactory: SpeciesDatabase is null.");
            return result;
        }

        if (unlockedStages == null || unlockedStages.Count == 0)
        {
            Debug.LogWarning("AnimalFactory: unlockedStages is null or empty. Defaulting to S1.");
            unlockedStages = new List<ContentStage> { ContentStage.S1 };
        }

        bool isStage1 = unlockedStages.Contains(ContentStage.S1) 
            && !unlockedStages.Contains(ContentStage.S2) 
            && !unlockedStages.Contains(ContentStage.S3);

        if (isFirstDay && isStage1)
        {
            // Guaranteed first guest: a rabbit with a reservation.
            SpeciesData rabbitData = database.Get("rabbit");
            if (rabbitData == null)
                Debug.LogError("AnimalFactory: 'rabbit' not found in SpeciesDatabase. Check speciesId.");
            else
            {
                string lastName = LastNames[Random.Range(0, LastNames.Length)];
                string firstName = FirstNames[Random.Range(0, FirstNames.Length)];
                int stayNights = Random.Range(MinNights, MaxNights + 1);
                result.Add(new Animal(rabbitData, lastName + firstName,
                    hasReservation: true, checkInDay: currentDay, stayNights: stayNights));
            }
        }

        int currentReservations = result.Count(a => a.hasReservation);

        int remaining = count - result.Count;
        for (int i = 0; i < remaining; i++)
        {
            bool forceNoReservation = currentReservations >= maxReservations;
            var animal = CreateAnimal(database, unlockedStages, currentDay, forceNoReservation);
            if (animal != null)
            {
                if (animal.hasReservation)
                    currentReservations++;
                result.Add(animal);
            }
        }

        foreach (var animal in result)
            Debug.Log(animal.ToString());

        return result;
    }
}