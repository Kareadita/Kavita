import {MetadataFieldChangeKind} from "./metadata-field-change-kind.enum";

export interface MetadataFieldChange {
  field: MetadataFieldChangeKind;
  from: unknown;
  to: unknown;
}
