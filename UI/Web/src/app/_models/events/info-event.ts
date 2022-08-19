import { EVENTS } from "src/app/_services/message-hub.service";

export interface InfoEvent {
    /**
     * Payload of the event subtype
     */
    body: any;
    /**
     * Subtype event
     */
    name: EVENTS.Info;
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
    eventType: 'single';
    /**
     * Type of progress. Helps widget understand how to display spinner
     */
    progress: 'none';
    /**
     * When event was sent
     */
    eventTime: string;
}