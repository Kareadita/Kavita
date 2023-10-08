import {SideNavStreamType} from "./sidenav-stream-type.enum";
import {Library, LibraryType} from "../library";

export interface SideNavStream {
  name: string;
  order: number;
  libraryId?: number;
  isProvided: boolean;
  streamType: SideNavStreamType;
  library: Library;
}
