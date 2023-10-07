import {SideNavStreamType} from "./sidenav-stream-type.enum";
import {LibraryType} from "../library";

export interface SideNavStream {
  name: string;
  order: number;
  libraryId?: number;
  isProvided: boolean;
  type: SideNavStreamType;
  libraryType?: LibraryType;
  libraryCover?: string;
}
