import { EVENTS } from "src/app/_services/message-hub.service";

export interface ProgressEvent {
    libraryId: number;
    progress: number;
    eventTime: string;
}