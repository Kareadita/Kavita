import {SideNavStreamType} from "./sidenav-stream-type.enum";

export interface SideNavStream {
  name: string;
  order: number;
  libraryId?: number;
  isProvided: boolean;
  type: SideNavStreamType;
}
