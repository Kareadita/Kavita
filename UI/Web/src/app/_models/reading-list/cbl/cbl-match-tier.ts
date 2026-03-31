export enum CblMatchTier {
  RemapRule = 0,
  ExternalId = 1,
  ExactName = 2,
  ComicVineNaming = 3,
  ArticleStripped = 4,
  ReprintStripped = 5,
  AlternateSeries = 6,
  UserDecision = 7,
  Unmatched = -1
}

export const allCblMatchTiers = Object.keys(CblMatchTier)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as CblMatchTier[];
