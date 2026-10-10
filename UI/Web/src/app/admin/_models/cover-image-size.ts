import {allEnums} from "../../_helpers/enum";

export enum CoverImageSize {
    Default = 1,
    Medium = 2,
    Large = 3,
    XLarge = 4
}

export const allCoverImageSizes = allEnums(CoverImageSize);
