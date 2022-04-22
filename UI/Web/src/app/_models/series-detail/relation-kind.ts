export enum RelationKind {
    Prequel = 1,
    Sequel = 2,
    SpinOff = 3,
    Adaptation = 4,
    SideStory = 5,
    Character = 6,
    Contains = 7,
    Other = 8
}

export const RelationKinds = [
    {text: 'Prequel', value: RelationKind.Prequel},
    {text: 'Sequel', value: RelationKind.Sequel},
    {text: 'Spin Off', value: RelationKind.SpinOff},
    {text: 'Adaptation', value: RelationKind.Adaptation},
    {text: 'Side Story', value: RelationKind.SideStory},
    {text: 'Character', value: RelationKind.Character},
    {text: 'Contains', value: RelationKind.Contains},
    {text: 'Other', value: RelationKind.Other},
];