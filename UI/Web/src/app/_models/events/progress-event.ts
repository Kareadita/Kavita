export interface ProgressEvent {
    libraryId: number;
    progress: number;
    eventTime: string;

    // New fields
    /**
     * Event type
     */
    name: string;
}