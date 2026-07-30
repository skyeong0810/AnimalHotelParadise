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
public class SpeciesDialogueLines
{
    [InspectorName("예약했다고 말할 때")]
    [Tooltip("예약 여부를 물었을 때 예약했다고 답하는 대사입니다.")]
    [TextArea(2, 4)]
    public string claimReservation;

    [InspectorName("이름과 종을 소개할 때")]
    [Tooltip("손님이 이름과 종을 말하는 대사입니다. {0}은 동물 이름, {1}은 종 이름으로 바뀌므로 그대로 남겨두세요.")]
    [TextArea(2, 4)]
    public string tellInfo;

    [InspectorName("예약을 부정당해 화날 때")]
    [Tooltip("예약 손님의 예약을 부정했을 때 화내는 대사입니다.")]
    [TextArea(2, 4)]
    public string reactDeniedAngry;

    [InspectorName("화난 채 체크인할 때")]
    [Tooltip("예약을 부정당했던 손님이 결국 체크인하며 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitCheckinAngry;

    [InspectorName("화난 채 거절당할 때")]
    [Tooltip("예약을 부정당했던 손님이 입실도 거절당하고 떠날 때 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitRejectedAngry;

    [InspectorName("예약 확인을 애원할 때")]
    [Tooltip("예약을 부정당한 뒤 다시 확인해 달라고 애원하는 대사입니다.")]
    [TextArea(2, 4)]
    public string beg;

    [InspectorName("예약이 없다고 말할 때")]
    [Tooltip("예약 여부를 물었을 때 예약하지 않았다고 답하는 대사입니다.")]
    [TextArea(2, 4)]
    public string noReservation;

    [InspectorName("현장 손님이 애원할 때")]
    [Tooltip("예약 없이 찾아온 손님이 입실을 부탁하는 대사입니다.")]
    [TextArea(2, 4)]
    public string begWalkIn;

    [InspectorName("체크인하며 떠날 때")]
    [Tooltip("정상적으로 체크인한 손님이 카운터를 떠나며 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitCheckin;

    [InspectorName("스스로 돌아갈 때")]
    [Tooltip("손님이 입실하지 않고 돌아가며 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitLeave;

    [InspectorName("입실을 거절당할 때")]
    [Tooltip("입실을 거절당한 손님이 떠나며 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitRejected;

    [InspectorName("방이 없어 거절당할 때")]
    [Tooltip("예약 손님이 빈 방이 없어 입실하지 못하고 떠날 때 하는 대사입니다.")]
    [TextArea(2, 4)]
    public string exitRejectedNoRoom;
}

[System.Serializable]
public class SpeciesData
{
    // ── Identity ──────────────────────────────
    [Tooltip("Internal species key used in code (matches ajong variable).")]
    public string speciesId;

    [Tooltip("Display name shown to the player (Korean).")]
    public string displayName;

    [Tooltip("The sprite representation of this species.")]
    public Sprite speciesSprite;

    [Tooltip("Back-facing sprite used when this species leaves through the lobby door.")]
    public Sprite backSprite;

    [Tooltip("Small face icon used in reservation lists.")]
    public Sprite reservationIconSprite;


    [Tooltip("Multiplier for this species when shown as the counter customer sprite. 1 = default size.")]
    [Min(0.1f)]
    public float counterSpriteScaleMultiplier = 1f;

    [Header("Dialogue")]
    [InspectorName("말버릇")]
    [Tooltip("Speech habit appended to this species' customer dialogue. Edit this in SpeciesDatabase to change the voice style.")]
    public string speechHabit;

    [InspectorName("상황별 대사")]
    [Tooltip("Optional situation-specific dialogue for this species. Empty fields use the shared default dialogue.")]
    public SpeciesDialogueLines dialogueLines = new SpeciesDialogueLines();

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

    [Tooltip("Probability (0-100) that this species actually damages the room on checkout.")]
    [Range(0, 100)]
    public int damageProbability;
}

// ─────────────────────────────────────────────
//  Species database (ScriptableObject)
// ─────────────────────────────────────────────

