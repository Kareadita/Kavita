import {HightlightColor} from "./annotation";

export interface CreateAnnotationRequest {
  libraryId: number;
  volumeId: number;
  chapterId: number;
  xpath: string;
  endingXPath: string | null;
  selectedText: string | null;
  comment: string | null;
  hightlightColor: HightlightColor;
  highlightCount: number;
  containsSpoiler: boolean;
  pageNumber: number;
  /**
   * Ui Only - the full paragraph of selected context
   */
  context: string | null;
}
