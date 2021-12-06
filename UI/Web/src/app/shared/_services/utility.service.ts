import { Injectable } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { Series } from 'src/app/_models/series';
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
  B = 'b',
  BACKSPACE = 'Backspace',
  DELETE = 'Delete',
  SHIFT = 'Shift'
}

export enum Breakpoint {
  Mobile = 768,
  Tablet = 1280,
  Desktop = 1440
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

  /**
   * Formats a Chapter name based on the library it's in
   * @param libraryType 
   * @param includeHash For comics only, includes a # which is used for numbering on cards
   * @param includeSpace Add a space at the end of the string. if includeHash and includeSpace are true, only hash will be at the end.
   * @returns 
   */
  formatChapterName(libraryType: LibraryType, includeHash: boolean = false, includeSpace: boolean = false) {
    switch(libraryType) {
      case LibraryType.Book:
        return 'Book' + (includeSpace ? ' ' : '');
      case LibraryType.Comic:
        if (includeHash) {
          return 'Issue #';
        }
        return 'Issue' + (includeSpace ? ' ' : '');
      case LibraryType.Manga:
        return 'Chapter' + (includeSpace ? ' ' : '');
    }
  }

  cleanSpecialTitle(title: string) {
    let cleaned = title.replace(/_/g, ' ').replace(/SP\d+/g, '').trim();
    cleaned = cleaned.substring(0, cleaned.lastIndexOf('.'));
    if (cleaned.trim() === '') {
      return title;
    }
    return cleaned;
  }

  mangaFormat(format: MangaFormat): string {
    switch (format) {
      case MangaFormat.EPUB:
        return 'EPUB';
      case MangaFormat.ARCHIVE:
        return 'Archive';
      case MangaFormat.IMAGE:
        return 'Image';
      case MangaFormat.PDF:
        return 'PDF';
      case MangaFormat.UNKNOWN:
        return 'Unknown';
    }
  }

  mangaFormatIcon(format: MangaFormat): string {
    switch (format) {
      case MangaFormat.EPUB:
        return 'fa-book';
      case MangaFormat.ARCHIVE:
        return 'fa-file-archive';
      case MangaFormat.IMAGE:
        return 'fa-image';
      case MangaFormat.PDF:
        return 'fa-file-pdf';
      case MangaFormat.UNKNOWN:
        return 'fa-question';
    }
  }

  isVolume(d: any) {
    return d != null && d.hasOwnProperty('chapters');
  }

  isChapter(d: any) {
    return d != null && d.hasOwnProperty('volumeId');
  }

  isSeries(d: any) {
    return d != null && d.hasOwnProperty('originalName');
  }

  asVolume(d: any) {
    return <Volume>d;
  }

  asChapter(d: any) {
    return <Chapter>d;
  }

  asSeries(d: any) {
    return <Series>d;
  }

  getActiveBreakpoint(): Breakpoint {
    if (window.innerWidth <= Breakpoint.Mobile) return Breakpoint.Mobile;
    else if (window.innerWidth > Breakpoint.Mobile && window.innerWidth <= Breakpoint.Tablet) return Breakpoint.Tablet;
    else if (window.innerWidth > Breakpoint.Tablet) return Breakpoint.Desktop
    
    return Breakpoint.Desktop;
  }

  isInViewport(element: Element, additionalTopOffset: number = 0) {
    const rect = element.getBoundingClientRect();
    return (
        rect.top >= additionalTopOffset &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
  }
}
