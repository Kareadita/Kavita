import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot } from '@angular/router';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { SeriesFilter } from 'src/app/_models/series-filter';
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
  F = 'f',
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

  filter(input: string, filter: string): boolean {
    if (input === null || filter === null) return false;
    const reg = /[_\.\-]/gi;
    return input.toUpperCase().replace(reg, '').includes(filter.toUpperCase().replace(reg, ''));
  }

  /**
   * Returns a new instance of a filterSettings that is populated with filter presets from URL
   * @param snapshot 
   * @param blankFilter Filter to start with 
   * @returns The Preset filter and if something was set within
   */
   filterPresetsFromUrl(snapshot: ActivatedRouteSnapshot, blankFilter: SeriesFilter): [SeriesFilter, boolean] {
    const filter = Object.assign({}, blankFilter);
    let anyChanged = false;

    const format = snapshot.queryParamMap.get('format');
    if (format !== undefined && format !== null) {
      filter.formats = [...filter.formats, ...format.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const genres = snapshot.queryParamMap.get('genres');
    if (genres !== undefined && genres !== null) {
      filter.genres = [...filter.genres, ...genres.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const ageRating = snapshot.queryParamMap.get('ageRating');
    if (ageRating !== undefined && ageRating !== null) {
      filter.ageRating = [...filter.ageRating, ...ageRating.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publicationStatus = snapshot.queryParamMap.get('publicationStatus');
    if (publicationStatus !== undefined && publicationStatus !== null) {
      filter.publicationStatus = [...filter.publicationStatus, ...publicationStatus.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const tags = snapshot.queryParamMap.get('tags');
    if (tags !== undefined && tags !== null) {
      filter.tags = [...filter.tags, ...tags.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const languages = snapshot.queryParamMap.get('languages');
    if (languages !== undefined && languages !== null) {
      filter.languages = [...filter.languages, ...languages.split(',')];
      anyChanged = true;
    }

    const writers = snapshot.queryParamMap.get('writers');
    if (writers !== undefined && writers !== null) {
      filter.writers = [...filter.writers, ...writers.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const artists = snapshot.queryParamMap.get('artists');
    if (artists !== undefined && artists !== null) {
      filter.artists = [...filter.artists, ...artists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const character = snapshot.queryParamMap.get('character');
    if (character !== undefined && character !== null) {
      filter.character = [...filter.character, ...character.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const colorist = snapshot.queryParamMap.get('colorist');
    if (colorist !== undefined && colorist !== null) {
      filter.colorist = [...filter.colorist, ...colorist.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const coverArtists = snapshot.queryParamMap.get('coverArtists');
    if (coverArtists !== undefined && coverArtists !== null) {
      filter.coverArtist = [...filter.coverArtist, ...coverArtists.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const editor = snapshot.queryParamMap.get('editor');
    if (editor !== undefined && editor !== null) {
      filter.editor = [...filter.editor, ...editor.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const inker = snapshot.queryParamMap.get('inker');
    if (inker !== undefined && inker !== null) {
      filter.inker = [...filter.inker, ...inker.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const letterer = snapshot.queryParamMap.get('letterer');
    if (letterer !== undefined && letterer !== null) {
      filter.letterer = [...filter.letterer, ...letterer.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const penciller = snapshot.queryParamMap.get('penciller');
    if (penciller !== undefined && penciller !== null) {
      filter.penciller = [...filter.penciller, ...penciller.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const publisher = snapshot.queryParamMap.get('publisher');
    if (publisher !== undefined && publisher !== null) {
      filter.publisher = [...filter.publisher, ...publisher.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    const translators = snapshot.queryParamMap.get('translators');
    if (translators !== undefined && translators !== null) {
      filter.translators = [...filter.translators, ...translators.split(',').map(item => parseInt(item, 10))];
      anyChanged = true;
    }

    /// Read status is encoded as true,true,true
    const readStatus = snapshot.queryParamMap.get('readStatus');
    if (readStatus !== undefined && readStatus !== null) {
      const values = readStatus.split(',').map(i => i === 'true');
      if (values.length === 3) {
        filter.readStatus.inProgress = values[0];
        filter.readStatus.notRead = values[1];
        filter.readStatus.read = values[2];
        anyChanged = true;
      }
    }

    const sortBy = snapshot.queryParamMap.get('sortBy');
    if (sortBy !== undefined && sortBy !== null) {
      const values = sortBy.split(',');
      if (values.length === 1) {
        values.push('true');
      }
      if (values.length === 2) {
        filter.sortOptions = {
          isAscending: values[1] === 'true',
          sortField: Number(values[0])
        }
        console.log('ascending: ', filter.sortOptions.isAscending)
        anyChanged = true;
      }
    }
    

    return [filter, anyChanged];
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

  getLibraryTypeIcon(format: LibraryType) {
    switch (format) {
      case LibraryType.Book:
        return 'fa-book';
      case LibraryType.Comic:
      case LibraryType.Manga:
        return 'fa-book-open';
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
