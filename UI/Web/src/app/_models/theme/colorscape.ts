export interface ColorScape {
  primary?: string;
  secondary?: string;
}

export enum ColorScapeKind {
  Series = 'series',
  Volume = 'volume',
  Chapter = 'chapter',
  Person = 'person',
  ReadingList = 'reading-list',
}
