import {allEnums} from "../../_helpers/enum";

export enum PdfRenderResolution {
  Default = 1,
  High = 2,
  Ultra = 3,
}

export const allPdfRenderResolutions = allEnums(PdfRenderResolution);
