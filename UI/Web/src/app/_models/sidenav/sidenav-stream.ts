import {SideNavStreamType} from "./sidenav-stream-type.enum";
import {Library, LibraryType} from "../library";
import {CommonStream} from "../common-stream";

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
  externalSourceUrl?: string;

}
