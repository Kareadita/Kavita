export enum AgeRating {
    /**
     * This is not a valid state for Series/Chapters, but used for Restricted Profiles
     */
    NotApplicable = -1,
    Unknown = 0,
    AdultsOnly = 1,
    EarlyChildhood = 2,
    Everyone = 3,
    Everyone10Plus = 4,
    G = 5,
    KidsToAdults = 6,
    Mature = 7,
    Mature15Plus = 8,
    Mature17Plus = 9,
    RatingPending = 10,
    Teen = 11,
    X18Plus = 12
}