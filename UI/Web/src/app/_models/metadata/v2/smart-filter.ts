import {FilterEntityType} from "./filter-entity-type";

export interface SmartFilter {
  id: number;
  name: string;
  filter: string;
  entityType: FilterEntityType;
}
