export enum HightlightColor {
  Blue = 1,
  Green = 2,
}

export interface Annotation {
  id: number;
  xpath: string;
  endingXPath: string | null;
  selectedText: string | null;
  comment: string;
  hightlightColor: HightlightColor;
  containsSpoiler: boolean;
  pageNumber: number;


  chapterId: number;

  ownerUserId: number;
  ownerUsername: string;
  createdUtc: string;
  lastModifiedUtc: string;
}
