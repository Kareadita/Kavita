import { CblBookResult } from "./cbl-book-result";
import { CblImportResult } from "./cbl-import-result.enum";

export interface CblImportSummary {
    cblName: string;
    results: Array<CblBookResult>;
    success: CblImportResult;
    successfulInserts: Array<CblBookResult>;
}