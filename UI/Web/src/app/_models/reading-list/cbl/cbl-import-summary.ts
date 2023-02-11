import { Series } from "../../series";
import { CblBookResult } from "./cbl-book-result";
import { CblImportResult } from "./cbl-import-result.enum";

export interface CblConflictQuestion {
    seriesName: string;
    librariesIds: Array<number>;
}

export interface CblImportSummary {
    cblName: string;
    results: Array<CblBookResult>;
    success: CblImportResult;
    successfulInserts: Array<CblBookResult>;
    conflicts: Array<Series>;
    conflicts2: Array<CblConflictQuestion>;
    
}