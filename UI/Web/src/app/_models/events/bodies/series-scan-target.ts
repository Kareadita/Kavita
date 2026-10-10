/**
 * On ScanProgress frames of a ScanSeries job, null on a library scan
 */
export interface SeriesScanTarget {
  seriesId: number;
  seriesName: string;
}
