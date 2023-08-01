import { HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import {TranslocoService} from "@ngneat/transloco";

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
  F = 'f',
  H = 'h',
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

  constructor(private translocoService: TranslocoService) { }


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
        return this.translocoService.translate('common.book-num') + (includeSpace ? ' ' : '');
      case LibraryType.Comic:
        if (includeHash) {
          return this.translocoService.translate('common.issue-hash-num');
        }
        return this.translocoService.translate('common.issue-num') + (includeSpace ? ' ' : '');
      case LibraryType.Manga:
        return this.translocoService.translate('common.chapter-num') + (includeSpace ? ' ' : '');
    }
  }


  filter(input: string, filter: string): boolean {
    if (input === null || filter === null || input === undefined || filter === undefined) return false;
    const reg = /[_\.\-]/gi;
    return input.toUpperCase().replace(reg, '').includes(filter.toUpperCase().replace(reg, ''));
  }

  filterMatches(input: string, filter: string): boolean {
    if (input === null || filter === null || input === undefined || filter === undefined) return false;
    const reg = /[_\.\-]/gi;
    return input.toUpperCase().replace(reg, '') === filter.toUpperCase().replace(reg, '');
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

  deepEqual(object1: any, object2: any) {
    if ((object1 === null || object1 === undefined) && (object2 !== null || object2 !== undefined)) return false;
    if ((object2 === null || object2 === undefined) && (object1 !== null || object1 !== undefined)) return false;
    if (object1 === null && object2 === null) return true;
    if (object1 === undefined && object2 === undefined) return true;


    const keys1 = Object.keys(object1);
    const keys2 = Object.keys(object2);
    if (keys1.length !== keys2.length) {
      return false;
    }
    for (const key of keys1) {
      const val1 = object1[key];
      const val2 = object2[key];
      const areObjects = this.isObject(val1) && this.isObject(val2);
      if (
        areObjects && !this.deepEqual(val1, val2) ||
        !areObjects && val1 !== val2
      ) {
        return false;
      }
    }
    return true;
  }
  private isObject(object: any) {
    return object != null && typeof object === 'object';
  }

  addPaginationIfExists(params: HttpParams, pageNum?: number, itemsPerPage?: number) {
    if (pageNum !== null && pageNum !== undefined && itemsPerPage !== null && itemsPerPage !== undefined) {
      params = params.append('pageNumber', pageNum + '');
      params = params.append('pageSize', itemsPerPage + '');
    }
    return params;
  }

  createPaginatedResult(response: any, paginatedVariable: PaginatedResult<any[]> | undefined = undefined) {
    if (paginatedVariable === undefined) {
      paginatedVariable = new PaginatedResult();
    }
    if (response.body === null) {
      paginatedVariable.result = [];
    } else {
      paginatedVariable.result = response.body;
    }

    const pageHeader = response.headers?.get('Pagination');
    if (pageHeader !== null) {
      paginatedVariable.pagination = JSON.parse(pageHeader);
    }

    return paginatedVariable;
  }

  getWindowDimensions() {
    const windowWidth = window.innerWidth
                  || document.documentElement.clientWidth
                  || document.body.clientWidth;
    const windowHeight = window.innerHeight
                  || document.documentElement.clientHeight
                  || document.body.clientHeight;
    return [windowWidth, windowHeight];
  }
}
