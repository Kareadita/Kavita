export interface Language {
    isoCode: string;
    title: string;
}

export interface KavitaLocale {
  /**
   * isoCode aka what maps to the file on disk and what transloco loads
   */
  fileName: string;
  renderName: string;
  translationCompletion: number;
  isRtL: boolean;
  hash: string;
}
