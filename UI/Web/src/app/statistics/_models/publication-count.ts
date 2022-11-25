import { PublicationStatus } from "src/app/_models/metadata/publication-status";

export interface PublicationCount {
    value: PublicationStatus;
    count: number;
}
