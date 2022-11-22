export enum PersonRole {
    Other = 1,
    Artist = 2,
    Writer = 3,
    Penciller = 4,
    Inker = 5,
    Colorist = 6,
    Letterer = 7,
    CoverArtist = 8,
    Editor = 9,
    Publisher = 10,
    Character = 11,
    Translator = 12
}

export interface Person {
    id: number;
    name: string;
    role: PersonRole;
}