export interface Language {
    isoCode: string;
    title: string;
}

export interface KavitaLocale {
  fileName: string; // isoCode aka what maps to the file on disk and what transloco loads
  renderName: string;
  translationCompletion: number;
  isRtL: boolean;
  hash: string;
}
