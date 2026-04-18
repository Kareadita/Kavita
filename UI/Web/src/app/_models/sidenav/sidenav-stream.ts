import {SideNavStreamType} from "./sidenav-stream-type.enum";
import {Library} from "../library/library";
import {CommonStream} from "../common-stream";
import {ExternalSource} from "./external-source";
import {FilterEntityType} from "../metadata/v2/filter-entity-type";

export interface SideNavStream extends CommonStream {
  name: string;
  order: number;
  libraryId?: number;
  isProvided: boolean;
  streamType: SideNavStreamType;
  library?: Library;
  visible: boolean;
  smartFilterId: number;
  smartFilterEncoded?: string;
  externalSource?: ExternalSource;
  entityType: FilterEntityType;
}
