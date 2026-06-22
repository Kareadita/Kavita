import {MetadataFetchTrigger} from "./metadata-fetch-trigger.enum";

export interface KavitaPlusAuditMetadataExtras {
  coverUrl: string | null;
  issueNumber: string | null;
  personName: string | null;
  aliasAdded: string | null;
  fetchTrigger: MetadataFetchTrigger | null;
}
