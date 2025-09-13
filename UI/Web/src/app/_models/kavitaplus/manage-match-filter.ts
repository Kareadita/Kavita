import {MatchStateOption} from "./match-state-option";
import {LibraryType} from "../library/library";

export interface ManageMatchFilter {
  matchStateOption: MatchStateOption;
  libraryType: LibraryType | -1;
  searchTerm: string;
}
