import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber} from 'src/app/_models/chapter';
import {LibraryType} from 'src/app/_models/library/library';
import {Volume} from 'src/app/_models/volume';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

/**
 * This is primarily used for list item
 */
@Component({
  selector: 'app-entity-title',
  imports: [
      TranslocoModule,
      DefaultValuePipe
  ],
  templateUrl: './entity-title.component.html',
  styleUrls: ['./entity-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityTitleComponent {

  private readonly utilityService = inject(UtilityService);

  protected readonly LooseLeafOrSpecial = LooseLeafOrDefaultNumber + "";
  protected readonly LibraryType = LibraryType;

  /**
   * Library type for which the entity belongs
   */
  libraryType = input.required<LibraryType>();
  entity = input.required<Volume | Chapter>();
  /**
   * When generating the title, should this prepend 'Volume number' before the Chapter wording
   */
  includeVolume = input<boolean>(false);
  /**
   * When generating the title, should this prepend 'Chapter number' before the Chapter titlename
   */
  includeChapter = input<boolean>(false);
  /**
   * When a titleName (aka a title) is available on the entity, show it over Volume X Chapter Y
   */
  prioritizeTitleName = input<boolean>(true);
  /**
   * When there is no meaningful title to display and the chapter is just a single volume, show the volume number
   */
  fallbackToVolume = input<boolean>(true);

  isChapter = computed(() => this.utilityService.isChapter(this.entity()));

  titleName = computed(() => {
    const isChapter = this.isChapter();
    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.titleName || '';
    }

    const volume = this.entity() as Volume;
    let title = volume.name || '';
    if (volume.chapters.length > 0 && volume.chapters[0].titleName) {
      title += ' - ' + volume.chapters[0].titleName;
    }

    return title;
  });
  volumeTitle = computed(() => {
    const isChapter = this.isChapter();

    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.volumeTitle || '';
    }

    const volume = this.entity() as Volume;
    return volume.name || '';
  });

  number = computed(() => {
    const isChapter = this.isChapter();

    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.range || '';
    }

    const volume = this.entity() as Volume;
    return volume.name || '';
  });

  renderText = computed(() => {
    // Lift all signal dependencies to the top of the computed
    const libraryType = this.libraryType();
    const titleName = this.titleName();
    const prioritizeTitleName = this.prioritizeTitleName();
    const fallbackToVolume = this.fallbackToVolume();
    const isChapter = this.isChapter();
    const number = this.number();
    const volumeTitle = this.volumeTitle();
    const includeChapter = this.includeChapter();
    const includeVolume = this.includeVolume();

    switch (libraryType) {
      case LibraryType.Manga:
        return this.calculateMangaRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle, includeChapter, includeVolume);
      case LibraryType.Comic:
        return this.calculateComicRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle, includeChapter, includeVolume);
      case LibraryType.Book:
        return this.calculateBookRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle);
      case LibraryType.Images:
        return this.calculateImageRenderText(isChapter, number, volumeTitle, fallbackToVolume);
      case LibraryType.LightNovel:
        return this.calculateLightNovelRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle);
      case LibraryType.ComicVine:
        return this.calculateComicRenderText(titleName, prioritizeTitleName, fallbackToVolume, isChapter, number, volumeTitle, includeChapter, includeVolume);
      default:
        return '';
    }
  });


  private calculateBookRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string) {
    let renderText = '';
    if (titleName !== '' && prioritizeTitleName) {
      renderText = titleName;
    } else if (fallbackToVolume && isChapter) {
      renderText = translate('entity-title.single-volume');
    } else if (number === this.LooseLeafOrSpecial) {
      renderText = '';
    } else {
      renderText = translate('entity-title.book-num', {num: volumeTitle});
    }
    return renderText;
  }

  private calculateLightNovelRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string) {
    let renderText = '';
    if (titleName !== '' && prioritizeTitleName) {
      renderText = titleName;
    } else if (fallbackToVolume && isChapter) {
      renderText = translate('entity-title.single-volume');
    } else if (number === this.LooseLeafOrSpecial) {
      renderText = '';
    } else {
      const bookNum = isChapter ? number : volumeTitle;
      renderText = translate('entity-title.book-num', {num: bookNum});
    }
    return renderText;
  }

  private calculateMangaRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean, isChapter: boolean, number: string, volumeTitle: string, includeChapter: boolean, includeVolume: boolean) {
    let renderText = '';

    if (titleName !== '' && prioritizeTitleName) {
      if (isChapter && includeChapter) {
        if (number === this.LooseLeafOrSpecial) {
          renderText = translate('entity-title.chapter') + ' - ';
        } else {
          renderText = translate('entity-title.chapter') + ' ' + number + ' - ';
        }
      }

      renderText += titleName;
    } else {
      if (includeVolume && volumeTitle !== '') {
        if (number !== this.LooseLeafOrSpecial && isChapter && includeVolume) {
          renderText = volumeTitle;
        }
      }

      if (number !== this.LooseLeafOrSpecial) {
        if (isChapter) {
          renderText = translate('entity-title.chapter') + ' ' + number;
        } else {
          renderText = volumeTitle;
        }
      } else if (fallbackToVolume && isChapter && volumeTitle) {
        renderText = translate('entity-title.vol-num', {num: volumeTitle});
      } else if (fallbackToVolume && isChapter) {
        renderText = translate('entity-title.single-volume');
      } else {
        renderText = translate('entity-title.special');
      }
    }

    return renderText;
  }

  private calculateImageRenderText(isChapter: boolean, number: string, volumeTitle: string, fallbackToVolume: boolean) {
    let renderText = '';

    if (number !== this.LooseLeafOrSpecial) {
      if (isChapter) {
        renderText = translate('entity-title.chapter') + ' ' + number;
      } else {
        renderText = volumeTitle;
      }
    } else {
      renderText = translate('entity-title.special');
    }

    return renderText;
  }

  private calculateComicRenderText(titleName: string, prioritizeTitleName: boolean, fallbackToVolume: boolean,
                                   isChapter: boolean, number: string, volumeTitle: string, includeChapter: boolean,
                                   includeVolume: boolean) {
    let renderText = '';

    // If titleName is provided and prioritized
    if (titleName && prioritizeTitleName) {
      if (isChapter && includeChapter) {
        renderText = translate('entity-title.issue-num') + ' ' + number + ' - ';
      }
      renderText += titleName;
    } else {
      // Otherwise, check volume and number logic
      if (includeVolume && volumeTitle) {
        if (number !== this.LooseLeafOrSpecial) {
          renderText = isChapter ? volumeTitle : '';
        }
      }
      // Render either issue number or volume title, or "special" if applicable
      renderText += number !== this.LooseLeafOrSpecial
        ? (isChapter ? translate('entity-title.issue-num') + ' ' + number : volumeTitle)
        : translate('entity-title.special');
    }

    return renderText;
  }
}
