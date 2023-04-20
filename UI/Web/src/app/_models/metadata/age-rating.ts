export enum AgeRating {
    /**
     * This is not a valid state for Series/Chapters, but used for Restricted Profiles
     */
    NotApplicable = -1,
    Unknown = 0,
    RatingPending = 1,
    EarlyChildhood = 2,
    Everyone = 3,
    G = 4,
    Everyone10Plus = 5,
    PG = 6,
    KidsToAdults = 7,
    Teen = 8,
    Mature15Plus = 9,
    Mature17Plus = 10,
    Mature = 11,
    R18Plus = 12,
    AdultsOnly = 13,
    X18Plus = 14
}