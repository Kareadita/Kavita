
export interface Annotation {
  id: number;
  xPath: string;
  endingXPath: string | null;
  selectedText: string | null;
  comment: string;
  containsSpoiler: boolean;
  pageNumber: number;
  selectedSlotIndex: number;
  chapterTitle: string | null;
  highlightCount: number;
  ownerUserId: number;
  ownerUsername: string;
  createdUtc: string;
  lastModifiedUtc: string;
  /**
   * A calculated selection of the surrounding text. This does not update after creation.
   */
  context: string | null;
  chapterId: number;
  libraryId: number;
  volumeId: number;
  seriesId: number;

}
