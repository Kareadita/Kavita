import { Injectable } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Volume } from 'src/app/_models/volume';

export enum KEY_CODES {
  RIGHT_ARROW = 'ArrowRight',
  LEFT_ARROW = 'ArrowLeft',
  DOWN_ARROW = 'ArrowDown',
  UP_ARROW = 'ArrowUp',
  ESC_KEY = 'Escape',
  SPACE = ' ',
  ENTER = 'Enter',
  G = 'g',
  BACKSPACE = 'Backspace',
  DELETE = 'Delete'
}

@Injectable({
  providedIn: 'root'
})
export class UtilityService {

  mangaFormatKeys: string[] = [];

  constructor() { }

  sortVolumes = (a: Volume, b: Volume) => {
    if (a === b) { return 0; }
    else if (a.number === 0) { return 1; }
    else if (b.number === 0) { return -1; }
    else {
      return a.number < b.number ? -1 : 1;
    }
  }

  sortChapters = (a: Chapter, b: Chapter) => {
    return parseFloat(a.number) - parseFloat(b.number);
  }

  mangaFormatToText(format: MangaFormat): string {
    if (this.mangaFormatKeys === undefined || this.mangaFormatKeys.length === 0) {
      this.mangaFormatKeys = Object.keys(MangaFormat);
    }

    return this.mangaFormatKeys.filter(item => MangaFormat[format] === item)[0];
  }

  cleanSpecialTitle(title: string) {
    let cleaned = title.replace(/_/g, ' ').replace(/SP\d+/g, '').trim();
    cleaned = cleaned.substring(0, cleaned.lastIndexOf('.'));
    if (cleaned.trim() === '') {
      return title;
    }
    return cleaned;
  }

}
