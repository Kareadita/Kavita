export interface Volume {
    id: number;
    number: string;
    files: Array<string>; // In future, we can refactor this to be a type with extra metadata around it
}