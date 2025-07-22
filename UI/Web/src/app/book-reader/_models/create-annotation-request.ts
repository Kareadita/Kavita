export interface CreateAnnotationRequest {
  libraryId: number;
  seriesId: number;
  volumeId: number;
  chapterId: number;
  xpath: string;
  endingXPath: string | null;
  selectedText: string | null;
  comment: string | null;
  highlightCount: number;
  containsSpoiler: boolean;
  pageNumber: number;
  selectedSlotIndex: number;

  /**
   * Ui Only - the full paragraph of selected context
   */
  context: string | null;
}
