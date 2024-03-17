export enum RelationKind {
    Prequel = 1,
    Sequel = 2,
    SpinOff = 3,
    Adaptation = 4,
    SideStory = 5,
    Character = 6,
    Contains = 7,
    Other = 8,
    AlternativeSetting = 9,
    AlternativeVersion = 10,
    Doujinshi = 11,
    /**
     * This is UI only. Backend will generate Parent series for everything but Prequel/Sequel
     */
    Parent = 12,
    Edition = 13,
    Annual = 14
}

const RelationKindsUnsorted = [
    {text: 'Prequel', value: RelationKind.Prequel},
    {text: 'Sequel', value: RelationKind.Sequel},
    {text: 'Spin Off', value: RelationKind.SpinOff},
    {text: 'Adaptation', value: RelationKind.Adaptation},
    {text: 'Annual', value: RelationKind.Annual},
    {text: 'Alternative Setting', value: RelationKind.AlternativeSetting},
    {text: 'Alternative Version', value: RelationKind.AlternativeVersion},
    {text: 'Side Story', value: RelationKind.SideStory},
    {text: 'Character', value: RelationKind.Character},
    {text: 'Contains', value: RelationKind.Contains},
    {text: 'Edition', value: RelationKind.Edition},
    {text: 'Doujinshi', value: RelationKind.Doujinshi},
    {text: 'Other', value: RelationKind.Other},
];

export const RelationKinds = RelationKindsUnsorted.slice().sort((a, b) => a.text.localeCompare(b.text));
