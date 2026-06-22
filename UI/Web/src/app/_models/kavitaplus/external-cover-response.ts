export enum ExternalCoverImageType
{
  Series,
  Volume,
  VolumeBack,
  Chapter,
  Issue,
  Banner,
  Season,
  Audiobook,
  Other
}

export interface ExternalCoverResponse {
  url: string;
  /** Only on MangaBaka responses */
  language?: string;
  type: ExternalCoverImageType;
  /** type dictates if volume or issue number */
  number: number;
}
