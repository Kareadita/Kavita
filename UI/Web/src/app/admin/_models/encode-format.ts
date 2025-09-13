export enum EncodeFormat {
    PNG = 0,
    WebP = 1,
    AVIF = 2
}

export const allEncodeFormats = Object.keys(EncodeFormat)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as EncodeFormat[];
