export interface NotificationContainer<T> {
    /**
     * Represents underlying type of event
     */
    type: string;
    /**
     * How many events are in this object
     */
     size: number;

     events: Array<T>;
}

export interface ActivityNotification {
    type: string; // library.update.section
    /**
     * If this notification has some sort of cancellable operation
     */
    cancellable: boolean;

    userId: number;
    /**
     * Main action title ie) Scanning LIBRARY_NAME
     */
    title: string;
    /**
     * Detail information about action. ie) Series Name
     */
    subtitle: string;
    /**
     * Progress of this action [0-100]
     */
    progress: number;
    /**
     * Any additional context backend needs to send to UI
     */
    context: {
        libraryId: number;
    };
}