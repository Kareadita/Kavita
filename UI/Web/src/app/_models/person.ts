export enum PersonRole {
    Other = 0,
    Author = 1,
    Artist = 2
}

export interface Person {
    name: string;
    role: PersonRole;
}