export interface Language {
    isoCode: string;
    title: string;
}

export interface KavitaLocale {
  fileName: string;
  renderName: string;
  translationCompletion: number;
  isRtL: boolean;
  hash: string;
}
