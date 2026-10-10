import {allEnums} from "../../_helpers/enum";

export enum EncodeFormat {
    PNG = 0,
    WebP = 1,
    AVIF = 2
}

export const allEncodeFormats = allEnums(EncodeFormat);
