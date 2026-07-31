using UnityEngine;

// ─────────────────────────────────────────────
//  Runtime animal instance
// ─────────────────────────────────────────────

/// <summary>
/// Represents one individual animal guest that visits the hotel.
/// Holds instance-level data (name, reservation, stay duration) while pointing
/// to its species' shared <see cref="SpeciesData"/> for all static traits.
///
/// Usage:
///   var guest = new Animal(db.Get("rabbit"), "김토깽이", hasReservation: true, checkInDay: 1, stayNights: 2);
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

    // ── Stay duration ─────────────────────────

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

    // ── Convenience pass-throughs ─────────────
    //    These let other systems read traits without drilling into .species

    public string SpeciesId => species?.speciesId;
    public Sprite ReservationIconSprite => species != null && species.reservationIconSprite != null
        ? species.reservationIconSprite
        : species?.speciesSprite;
    public float CounterSpriteScaleMultiplier => species != null ? Mathf.Max(0.1f, species.counterSpriteScaleMultiplier) : 1f;
    public DietType DietType => species?.dietType ?? DietType.Herbivore;
    public ActivityCycle Activity => species?.activityCycle ?? ActivityCycle.Diurnal;
    public int FloorNuisance => species?.floorNuisanceProbability ?? 0;
    public int WallNuisance => species?.wallNuisanceProbability ?? 0;
    public int SurroundNuisance => species?.surroundNuisanceProbability ?? 0;
    public bool RequiresSpecialRoom => species?.requiresSpecialRoom ?? false;
    public bool LeavesOdour => species?.leavesOdour ?? false;
    public bool CausesDamage => species?.causesDamage ?? false;
    public int DamageProbability => species?.damageProbability ?? 0;
    public bool IsCarnivore => DietType == DietType.Carnivore;
    public bool IsNocturnal => Activity == ActivityCycle.Nocturnal;

    // ── Instance Nuisance Determination ───────

    /// <summary>
    /// Whether nuisance probabilities have been evaluated for this guest instance.
    /// Once determined, these flags persist until the guest checks out.
    /// </summary>
    public bool hasDeterminedNuisance;
    public bool willCauseFloorNuisance;
    public bool willCauseWallNuisance;
    public bool willCauseSurroundNuisance;

    /// <summary>
    /// Whether this guest currently has an unanswered-for nuisance complaint outstanding. Set true the
    /// moment their call starts ringing; blocks any further call from being scheduled while true (see
    /// RoomManager.ScheduleNuisanceCall). Reset back to false once a promised room move actually
    /// happens (see RoomManager.MoveAnimal) — so the guest can complain again if the *new* room turns
    /// out to have its own nuisance problem. It is NOT reset when a complaint goes unresolved (no room
    /// offered, or the call was missed) — an unresolved guest never gets a second chance to call.
    /// </summary>
    public bool hasCalledNuisance;

    /// <summary>
    /// How many times this guest has actually placed a nuisance complaint call during their stay
    /// (i.e., how many times their call has started ringing). Unlike <see cref="hasCalledNuisance"/>,
    /// this never resets — DayManager.GetCheckoutRating uses it to tell "resolved cleanly on the first
    /// complaint" apart from "resolved, but the nuisance kept recurring after being moved" (the
    /// "부분 해결" rating tier).
    /// </summary>
    public int nuisanceComplaintCount;

    /// <summary>How a nuisance complaint (if any) for this guest was ultimately handled.</summary>
    public enum NuisanceResolution
    {
        /// <summary>This guest never had to call about a nuisance.</summary>
        None,
        /// <summary>Staff answered and moved the guest to a new room.</summary>
        Resolved,
        /// <summary>The call was missed, or staff answered but had no room to offer.</summary>
        Unresolved
    }

    /// <summary>
    /// Outcome of this guest's nuisance complaint, if they had one. Read at checkout time to
    /// determine the rating penalty (see DayManager.FinalizeCheckoutGuest).
    /// </summary>
    public NuisanceResolution nuisanceResolution = NuisanceResolution.None;

    /// <summary>
    /// The four checkout-grading tiers a guest can fall into, derived purely from their own
    /// complaint history (<see cref="nuisanceComplaintCount"/> and <see cref="nuisanceResolution"/>).
    /// Both the checkout rating (DayManager.GetCheckoutRating) and the checkout payment adjustment
    /// (DayManager.GetCheckoutAdjustment) key off this single classification so the tiering logic
    /// only has to live — and change — in one place.
    /// </summary>
    public enum CheckoutOutcome
    {
        /// <summary>Guest never had a nuisance complaint.</summary>
        NoIssue,
        /// <summary>Guest had exactly one complaint and it was resolved (moved) cleanly.</summary>
        Resolved,
        /// <summary>
        /// Guest was moved at least once, the nuisance recurred, but the most recent complaint was
        /// still ultimately resolved. Only reachable with nuisanceComplaintCount >= 2, since a guest
        /// can only place a second complaint after their first one was actually resolved (see
        /// RoomManager.MoveAnimal, which only resets hasCalledNuisance on a successful move).
        /// </summary>
        PartiallyResolved,
        /// <summary>
        /// The guest's most recent (and, per the game's call-scheduling rules, therefore final)
        /// complaint was never resolved — no room was offered, or the call was missed outright.
        /// </summary>
        Unresolved
    }

    /// <summary>
    /// Classifies this guest's stay into one of the four <see cref="CheckoutOutcome"/> tiers, based
    /// solely on their own complaint history. Safe to call at any time after checkout is decided;
    /// does not read or mutate any other guest or room state.
    /// </summary>
    public CheckoutOutcome GetCheckoutOutcome()
    {
        // A guest can only ever place a second complaint after their first one was actually resolved
        // (see RoomManager.MoveAnimal resetting hasCalledNuisance only on a successful move) — so
        // nuisanceComplaintCount >= 2 always means "moved at least once, then the nuisance recurred".
        // If their latest complaint also ended up resolved, that's the "부분 해결" tier: better than an
        // outright unresolved complaint, but worse than a single clean resolution since it happened
        // more than once. If the latest complaint is unresolved, it falls through to the normal
        // unresolved case below — being bounced around and then failed isn't graded any more leniently
        // than failing on the first try.
        if (nuisanceComplaintCount >= 2 && nuisanceResolution == NuisanceResolution.Resolved)
        {
            return CheckoutOutcome.PartiallyResolved;
        }

        switch (nuisanceResolution)
        {
            case NuisanceResolution.Resolved:
                return CheckoutOutcome.Resolved;
            case NuisanceResolution.Unresolved:
                return CheckoutOutcome.Unresolved;
            default:
                return CheckoutOutcome.NoIssue;
        }
    }

    /// <summary>
    /// Evaluates nuisance probabilities once per guest instance and saves the results.
    /// </summary>
    public void DetermineNuisance()
    {
        if (hasDeterminedNuisance) return;

        willCauseFloorNuisance = FloorNuisance > 0 && Random.Range(0, 100) < FloorNuisance;
        willCauseWallNuisance = WallNuisance > 0 && Random.Range(0, 100) < WallNuisance;
        willCauseSurroundNuisance = SurroundNuisance > 0 && Random.Range(0, 100) < SurroundNuisance;
        hasDeterminedNuisance = true;
    }

    // ── Constructor ───────────────────────────

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
        $"예약:{hasReservation} 식성:{DietType} 활동:{Activity} " +
        $"층간:{FloorNuisance}% 벽간:{WallNuisance}% 사방:{SurroundNuisance}% " +
        $"체크인:Day {checkInDay} 체크아웃:Day {CheckOutDay}";
}
