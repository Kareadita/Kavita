export interface CblItemDecision {
  seriesId: number;
  volumeId: number;
  chapterId: number;
}

export interface CblImportDecisions {
  itemResolutions: Record<number, CblItemDecision>;
  saveAsRemapRules: boolean;
}
