import { HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import {translate, TranslocoService} from "@jsverse/transloco";
import {debounceTime, ReplaySubject, shareReplay} from "rxjs";

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

  public readonly activeBreakpointSource = new ReplaySubject<Breakpoint>(1);
  public readonly activeBreakpoint$ = this.activeBreakpointSource.asObservable().pipe(debounceTime(60), shareReplay({bufferSize: 1, refCount: true}));

  mangaFormatKeys: string[] = [];


  sortChapters = (a: Chapter, b: Chapter) => {
    return a.minNumber - b.minNumber;
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
   * @param plural Pluralize word
   * @returns
   */
   formatChapterName(libraryType: LibraryType, includeHash: boolean = false, includeSpace: boolean = false, plural: boolean = false) {
    const extra = plural ? 's' : '';

     switch(libraryType) {
      case LibraryType.Book:
      case LibraryType.LightNovel:
        return translate('common.book-num' + extra) + (includeSpace ? ' ' : '');
      case LibraryType.Comic:
      case LibraryType.ComicVine:
        if (includeHash) {
          return translate('common.issue-hash-num');
        }
        return translate('common.issue-num' + extra) + (includeSpace ? ' ' : '');
      case LibraryType.Images:
      case LibraryType.Manga:
        return translate('common.chapter-num' + extra) + (includeSpace ? ' ' : '');
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

  deepEqual(object1: any | undefined | null, object2: any | undefined | null) {
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
