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
  containsSpoiler: boolean;
  pageNumber: number;
  selectedSlotIndex: number;


  chapterId: number;

  chapterTitle: string | null;
  /**
   * UI Only
   */
  surroundingText: string | null;

  ownerUserId: number;
  ownerUsername: string;
  createdUtc: string;
  lastModifiedUtc: string;
}
