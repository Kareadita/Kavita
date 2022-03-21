export interface LibraryModifiedEvent {
    libraryId: number;
    action: 'create' | 'delelte';
}