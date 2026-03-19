import {CblBookResult} from "./cbl-book-result";
import {CblImportResult} from "./cbl-import-result.enum";

export interface CblImportSummary {
    cblName: string;
    fileName: string;
    results: Array<CblBookResult>;
    success: CblImportResult;
    isUpdate: boolean;
    successfulInserts: Array<CblBookResult>;
}
