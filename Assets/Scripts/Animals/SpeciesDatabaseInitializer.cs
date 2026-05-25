// ─────────────────────────────────────────────────────────────────────────────
//  SpeciesDatabaseInitializer.cs
//
//  Editor-only helper that populates a SpeciesDatabase ScriptableObject
//  with all species defined in the AHP design doc (애호박_AHP.xlsx).
//
//  Usage:
//    1. Create an empty SpeciesDatabase asset.
//    2. Select it in the Inspector.
//    3. Right-click → "Populate with Default AHP Species"
//       (or call SpeciesDatabaseInitializer.Populate() from your own editor script).
//
//  All noise values are placeholders from the design doc examples —
//  adjust them in the Inspector once the full balance table is ready.
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpeciesDatabaseInitializer
{
    [MenuItem("AHP/Populate Species Database")]
    public static void PopulateFromMenu()
    {
        var db = Selection.activeObject as SpeciesDatabase;
        if (db == null)
        {
            Debug.LogWarning("Select a SpeciesDatabase asset first.");
            return;
        }
        Populate(db);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Populated {db.allSpecies.Count} species into {db.name}.");
    }

    /// <summary>Fills the database with the default AHP species roster.</summary>
    public static void Populate(SpeciesDatabase db)
    {
        db.allSpecies = new List<SpeciesData>
        {
            // ── S1 ────────────────────────────────────────────────────────
            new SpeciesData
            {
                speciesId             = "squirrel",
                displayName           = "다람쥐",
                stage                 = ContentStage.S1,
                dietType              = DietType.Herbivore,
                activityCycle         = ActivityCycle.Diurnal,
                floorNoiseProbability = 0,   // design doc example: 다람쥐 0%
                wallNoiseProbability  = 0,
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "roe_deer",
                displayName           = "고라니",
                stage                 = ContentStage.S1,
                dietType              = DietType.Herbivore,
                activityCycle         = ActivityCycle.Diurnal,
                floorNoiseProbability = 30,
                wallNoiseProbability  = 50,  // design doc example: 고라니 50%
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "mouse",
                displayName           = "쥐",
                stage                 = ContentStage.S1,
                dietType              = DietType.Herbivore,    // adjust if design changes
                activityCycle         = ActivityCycle.Nocturnal,
                floorNoiseProbability = 10,
                wallNoiseProbability  = 10,
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = true,  // 쥐 → broken = T
            },
            new SpeciesData
            {
                speciesId             = "rabbit",
                displayName           = "토끼",
                stage                 = ContentStage.S1,
                dietType              = DietType.Herbivore,
                activityCycle         = ActivityCycle.Diurnal,
                floorNoiseProbability = 70,  // design doc example: 토끼 70%
                wallNoiseProbability  = 0,
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },

            // ── S2 ────────────────────────────────────────────────────────
            new SpeciesData
            {
                speciesId             = "sheep",
                displayName           = "양",
                stage                 = ContentStage.S2,
                dietType              = DietType.Herbivore,
                activityCycle         = ActivityCycle.Diurnal,
                floorNoiseProbability = 20,
                wallNoiseProbability  = 10,
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "cat",
                displayName           = "고양이",
                stage                 = ContentStage.S2,
                dietType              = DietType.Carnivore,
                activityCycle         = ActivityCycle.Nocturnal,
                floorNoiseProbability = 5,
                wallNoiseProbability  = 5,
                surroundNoiseProbability = 10,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "skunk",
                displayName           = "스컹크",
                stage                 = ContentStage.S2,
                dietType              = DietType.Carnivore,    // adjust if design changes
                activityCycle         = ActivityCycle.Nocturnal,
                floorNoiseProbability = 0,
                wallNoiseProbability  = 0,
                surroundNoiseProbability = 0,
                requiresSpecialRoom   = false,
                leavesOdour           = true,  // 스컹크 → smell = T
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "wolf",
                displayName           = "늑대",
                stage                 = ContentStage.S2,
                dietType              = DietType.Carnivore,
                activityCycle         = ActivityCycle.Nocturnal,
                floorNoiseProbability = 30,
                wallNoiseProbability  = 20,
                surroundNoiseProbability = 40,  // design doc example: 늑대 40%
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },

            // ── S3 ────────────────────────────────────────────────────────
            new SpeciesData
            {
                speciesId             = "tiger",
                displayName           = "호랑이",
                stage                 = ContentStage.S3,
                dietType              = DietType.Carnivore,
                activityCycle         = ActivityCycle.Nocturnal,
                floorNoiseProbability = 50,
                wallNoiseProbability  = 30,
                surroundNoiseProbability = 20,
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            new SpeciesData
            {
                speciesId             = "chicken",
                displayName           = "닭",
                stage                 = ContentStage.S3,
                dietType              = DietType.Herbivore,
                activityCycle         = ActivityCycle.Diurnal,
                floorNoiseProbability = 10,
                wallNoiseProbability  = 5,
                surroundNoiseProbability = 30,  // 닭 → anoise (디자인 문서 참고)
                requiresSpecialRoom   = false,
                leavesOdour           = false,
                causesDamage          = false,
            },
            // Add more S3 species here as the design doc grows (@...)
        };

        db.BuildLookup();
    }
}
#endif