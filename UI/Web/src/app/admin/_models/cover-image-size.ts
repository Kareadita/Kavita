
export enum CoverImageSize {
    Default = 1,
    Medium = 2,
    Large = 3,
    XLarge = 4
}

export const allCoverImageSizes = Object.keys(CoverImageSize)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as CoverImageSize[];
