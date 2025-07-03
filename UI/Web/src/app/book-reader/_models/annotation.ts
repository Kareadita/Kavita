export enum HightlightColor {
  Blue = 1,
  Green = 2,
}

export interface Annotation {
  id: number;
  xpath: string;
  endingXPath: string | null;
  selectedText: string | null;
  noteText: string;
  highlightCount: number;
  hightlightColor: HightlightColor;

  seriesId: number;
  volumeId: number;
  chapterId: number;

  createdUtc: string;
  lastModifiedUtc: string;
}
