import {SideNavStreamType} from "./sidenav-stream-type.enum";
import {Library} from "../library/library";
import {CommonStream} from "../common-stream";
import {ExternalSource} from "./external-source";

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

}
