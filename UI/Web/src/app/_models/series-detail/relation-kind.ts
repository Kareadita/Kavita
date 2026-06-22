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
    Annual = 14,

    Main = 15,
    Cameo = 16,
    CharacterFocus = 17,
    Compilation = 18,
    Crossover = 19,
    Expansion = 20,
    Parody = 21,
    Reboot = 22,
    Remake = 23,
    Series = 24,
    Source = 25,
    Summary = 26,
    Uncollected = 27

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
    {text: 'Main Story', value: RelationKind.Main},
    {text: 'Cameo', value: RelationKind.Cameo},
    {text: 'Character Focus', value: RelationKind.CharacterFocus},
    {text: 'Compilation', value: RelationKind.Compilation},
    {text: 'Crossover', value: RelationKind.Crossover},
    {text: 'Expansion', value: RelationKind.Expansion},
    {text: 'Parody', value: RelationKind.Parody},
    {text: 'Reboot', value: RelationKind.Reboot},
    {text: 'Remake', value: RelationKind.Remake},
    {text: 'Series', value: RelationKind.Series},
    {text: 'Source', value: RelationKind.Source},
    {text: 'Summary', value: RelationKind.Summary},
    {text: 'Uncollected', value: RelationKind.Uncollected},
];

export const RelationKinds = RelationKindsUnsorted.slice().sort((a, b) => a.text.localeCompare(b.text));
