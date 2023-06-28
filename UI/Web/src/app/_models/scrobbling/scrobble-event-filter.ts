export enum ScrobbleEventSortField {
  None = 0,
  Created = 1,
  LastModified = 2,
  Type= 3,
  Series = 4,
  IsProcessed = 5
}

export interface ScrobbleEventFilter {
  field: ScrobbleEventSortField;
  isDescending: boolean;
  query?: string;
}
