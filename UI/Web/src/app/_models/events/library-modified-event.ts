export interface LibraryModifiedEvent {
    libraryId: number;
    action: 'create' | 'delete';
}
