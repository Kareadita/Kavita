import {allEnums} from "../../_helpers/enum";

export enum TagWeight {
  Core = 1,
  Defining = 2,
  Recurrent = 3,
  Incidental = 4,
  Unweighted = 5,
}

export const allTagWeights = allEnums(TagWeight);
