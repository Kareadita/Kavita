import { CblImportReason } from "./cbl-import-reason.enum";

export interface CblBookResult {
    order: number;
    series: string;
    volume: string;
    number: string;
    reason: CblImportReason;
}