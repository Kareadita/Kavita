import {inject, Injectable} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {UtilityService} from '../shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber} from '../_models/chapter';
import {LibraryType} from '../_models/library/library';
import {Volume} from '../_models/volume';

const LooseLeafOrSpecial = LooseLeafOrDefaultNumber + '';

@Injectable({ providedIn: 'root' })
export class EntityTitleService {
  private readonly translocoService = inject(TranslocoService);
  private readonly utilityService = inject(UtilityService);

  computeTitle(
    entity: Volume | Chapter,
    libraryType: LibraryType | number,
    options?: {
      prioritizeTitleName?: boolean;
      fallbackToVolume?: boolean;
      includeChapter?: boolean;
      includeVolume?: boolean;
    }
  ): string {
    const prioritizeTitleName = options?.prioritizeTitleName ?? true;
    const fallbackToVolume = options?.fallbackToVolume ?? true;
    const includeChapter = options?.includeChapter ?? false;
    const includeVolume = options?.includeVolume ?? false;

    const isChapter = this.utilityService.isChapter(entity);

    // Special chapters always display their title directly
    if (isChapter && (entity as Chapter).isSpecial) {
      const chapter = entity as Chapter;
      return chapter.title || chapter.range || '';
    }

    // Compute titleName
    let titleName = '';
    if (isChapter) {
      titleName = (entity as Chapter).titleName || '';
    } else {
      const volume = entity as Volume;
      let title = volume.name || '';
      if (volume.chapters.length > 0 && volume.chapters[0].titleName) {
        title += ' - ' + volume.chapters[0].titleName;
      }
      titleName = title;
    }

    // Compute number (range for chapter, name for volume)
    const number = isChapter ? (entity as Chapter).range || '' : (entity as Volume).name || '';

    // Compute volumeTitle
    const volumeTitle = isChapter
      ? (entity as Chapter).volumeTitle || ''
      : (entity as Volume).name || '';

    switch (libraryType) {
      case LibraryType.Manga:
        return this.calculateMangaRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle, includeChapter, includeVolume);
      case LibraryType.Comic:
      case LibraryType.ComicVine:
        return this.calculateComicRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle, includeChapter, includeVolume);
      case LibraryType.Book:
        return this.calculateBookRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle);
      case LibraryType.Images:
        return this.calculateImageRenderText(isChapter, number, volumeTitle, fallbackToVolume);
      case LibraryType.LightNovel:
        return this.calculateLightNovelRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle);
      default:
        return '';
    }
  }

  private calculateBookRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string): string {
    let renderText = '';
    if (titleName !== '' && prioritizeTitleName) {
      renderText = titleName;
    } else if (fallbackToVolume && isChapter) {
      renderText = this.translocoService.translate('entity-title.single-volume');
    } else if (number === LooseLeafOrSpecial) {
      renderText = '';
    } else {
      renderText = this.translocoService.translate('entity-title.book-num', {num: volumeTitle});
    }
    return renderText;
  }

  private calculateLightNovelRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string): string {
    let renderText = '';
    if (titleName !== '' && prioritizeTitleName) {
      renderText = titleName;
    } else if (fallbackToVolume && isChapter) {
      renderText = this.translocoService.translate('entity-title.single-volume');
    } else if (number === LooseLeafOrSpecial) {
      renderText = '';
    } else {
      const bookNum = isChapter ? number : volumeTitle;
      renderText = this.translocoService.translate('entity-title.book-num', {num: bookNum});
    }
    return renderText;
  }

  private calculateMangaRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string, includeChapter: boolean, includeVolume: boolean): string {
    let renderText = '';

    if (titleName !== '' && prioritizeTitleName) {
      if (isChapter && includeChapter) {
        if (number === LooseLeafOrSpecial) {
          renderText = this.translocoService.translate('entity-title.chapter') + ' - ';
        } else {
          renderText = this.translocoService.translate('entity-title.chapter', {num: number}) + ' - ';
        }
      }
      renderText += titleName;
    } else {
      if (includeVolume && volumeTitle !== '') {
        if (number !== LooseLeafOrSpecial && isChapter && includeVolume) {
          renderText = volumeTitle;
        }
      }

      if (number !== LooseLeafOrSpecial) {
        if (isChapter) {
          renderText = this.translocoService.translate('entity-title.chapter', {num: number});
        } else {
          renderText = volumeTitle;
        }
      } else if (fallbackToVolume && isChapter && volumeTitle) {
        renderText = this.translocoService.translate('entity-title.vol-num', {num: volumeTitle});
      } else if (fallbackToVolume && isChapter) {
        renderText = this.translocoService.translate('entity-title.single-volume');
      } else {
        renderText = this.translocoService.translate('entity-title.special');
      }
    }

    return renderText;
  }

  private calculateImageRenderText(isChapter: boolean, number: string, volumeTitle: string, fallbackToVolume: boolean): string {
    let renderText = '';

    if (number !== LooseLeafOrSpecial) {
      if (isChapter) {
        renderText = this.translocoService.translate('entity-title.chapter', {num: number});
      } else {
        renderText = volumeTitle;
      }
    } else {
      renderText = this.translocoService.translate('entity-title.special');
    }

    return renderText;
  }

  private calculateComicRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean,
                                   isChapter: boolean, number: string, volumeTitle: string, includeChapter: boolean,
                                   includeVolume: boolean): string {
    let renderText = '';

    if (titleName && prioritizeTitleName) {
      if (isChapter && includeChapter) {
        renderText = this.translocoService.translate('entity-title.issue-num', {num: number}) + ' - ';
      }
      renderText += titleName;
    } else {
      if (includeVolume && volumeTitle) {
        if (number !== LooseLeafOrSpecial) {
          renderText = isChapter ? volumeTitle : '';
        }
      }
      renderText += number !== LooseLeafOrSpecial
        ? (isChapter ? this.translocoService.translate('entity-title.issue-num', {num: number}) : volumeTitle)
        : this.translocoService.translate('entity-title.special');
    }

    return renderText;
  }
}
