export enum PdfRenderResolution {
  Default = 1,
  High = 2,
  Ultra = 3,
}

export const allPdfRenderResolutions = Object.keys(PdfRenderResolution)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as PdfRenderResolution[];
