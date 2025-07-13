export enum HighlightColor {
  Blue = 1,
  Green = 2,
}

export const allHighlightColors = [HighlightColor.Blue, HighlightColor.Green];

export interface Annotation {
  id: number;
  xpath: string;
  endingXPath: string | null;
  selectedText: string | null;
  comment: string;
  highlightColor: HighlightColor;
  containsSpoiler: boolean;
  pageNumber: number;


  chapterId: number;

  ownerUserId: number;
  ownerUsername: string;
  createdUtc: string;
  lastModifiedUtc: string;
}
