import { CblImportReason } from "./cbl-import-reason.enum";

export interface CblBookResult {
    series: string;
    volume: string;
    number: string;
    reason: CblImportReason;
}