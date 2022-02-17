export interface NotificationProgressEvent {
    /**
     * Payload of the event subtype
     */
    body: any;
    /**
     * Subtype event
     */
    name: string;
    /**
     * Title to display in events widget
     */
    title: string;
    /**
     * Optional subtitle to display. Defaults to empty string
     */
    subTitle: string;
    /**
     * Type of event. Helps events widget to understand how to handle said event
     */
    eventType: 'single' | 'started' | 'updated' | 'ended';
    /**
     * When event was sent
     */
    eventTime: string;
}