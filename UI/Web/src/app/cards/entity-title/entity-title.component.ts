import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter, LooseLeafOrDefaultNumber } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library/library';
import { Volume } from 'src/app/_models/volume';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

/**
 * This is primarily used for list item
 */
@Component({
  selector: 'app-entity-title',
  standalone: true,
  imports: [
    TranslocoModule,
    DefaultValuePipe
  ],
  templateUrl: './entity-title.component.html',
  styleUrls: ['./entity-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityTitleComponent implements OnInit {

  private readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);

  protected readonly LooseLeafOrSpecial = LooseLeafOrDefaultNumber + "";
  protected readonly LibraryType = LibraryType;

  /**
   * Library type for which the entity belongs
   */
  @Input() libraryType: LibraryType = LibraryType.Manga;
  @Input({required: true}) entity!: Volume | Chapter;
  /**
   * When generating the title, should this prepend 'Volume number' before the Chapter wording
   */
  @Input() includeVolume: boolean = false;
  /**
   * When generating the title, should this prepend 'Chapter number' before the Chapter titlename
   */
  @Input() includeChapter: boolean = false;
  /**
   * When a titleName (aka a title) is available on the entity, show it over Volume X Chapter Y
   */
  @Input() prioritizeTitleName: boolean = true;
  /**
   * When there is no meaningful title to display and the chapter is just a single volume, show the volume number
   */
  @Input() fallbackToVolume: boolean = true;

  isChapter = false;
  titleName: string = '';
  volumeTitle: string = '';
  number: string = '';
  renderText: string = '';


  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);

    if (this.isChapter) {
      const c = (this.entity as Chapter);
      this.volumeTitle = c.volumeTitle || '';
      this.titleName = c.titleName || '';
      this.number = c.range;
    } else {
      const v = this.utilityService.asVolume(this.entity);
      this.volumeTitle = v.name || '';
      this.titleName = v.name || '';
      if (v.chapters[0].titleName) {
        this.titleName += ' - ' + v.chapters[0].titleName;
      }
      this.number = v.name;
    }

    this.calculateRenderText();

    this.cdRef.markForCheck();
  }

  private calculateRenderText() {
    switch (this.libraryType) {
      case LibraryType.Manga:
        this.renderText = this.calculateMangaRenderText();
        break;
      case LibraryType.Comic:
        this.renderText = this.calculateComicRenderText();
        break;
      case LibraryType.Book:
        this.renderText = this.calculateBookRenderText();
        break;
      case LibraryType.Images:
        this.renderText = this.calculateImageRenderText();
        break;
      case LibraryType.LightNovel:
        this.renderText = this.calculateLightNovelRenderText();
        break;
      case LibraryType.ComicVine:
        this.renderText = this.calculateComicRenderText();
        break;
    }
    this.cdRef.markForCheck();
  }

  private calculateBookRenderText() {
    let renderText = '';
    if (this.titleName !== '' && this.prioritizeTitleName) {
      renderText = this.titleName;
    } else if (this.fallbackToVolume && this.isChapter) { // (his is a single volume on volume detail page
      renderText = translate('entity-title.single-volume');
    } else if (this.number === this.LooseLeafOrSpecial) {
      renderText = '';
    } else {
      renderText = translate('entity-title.book-num', {num: this.volumeTitle});
    }
    return renderText;
  }

  private calculateLightNovelRenderText() {
    let renderText = '';
    if (this.titleName !== '' && this.prioritizeTitleName) {
      renderText = this.titleName;
    } else if (this.fallbackToVolume && this.isChapter) { // (his is a single volume on volume detail page
      renderText = translate('entity-title.single-volume');
    } else if (this.number === this.LooseLeafOrSpecial) {
      renderText = '';
    } else {
      const bookNum = this.isChapter ? this.number : this.volumeTitle;
      renderText = translate('entity-title.book-num', {num: bookNum});
    }
    return renderText;
  }

  private calculateMangaRenderText() {
    let renderText = '';

    if (this.titleName !== '' && this.prioritizeTitleName) {
      if (this.isChapter && this.includeChapter) {
        if (this.number === this.LooseLeafOrSpecial) {
          renderText = translate('entity-title.chapter') + ' - ';
        } else {
          renderText = translate('entity-title.chapter') + ' ' + this.number + ' - ';
        }
      }

      renderText += this.titleName;
    } else {
      if (this.includeVolume && this.volumeTitle !== '') {
        if (this.number !== this.LooseLeafOrSpecial && this.isChapter && this.includeVolume) {
          renderText = this.volumeTitle;
        }
      }

      if (this.number !== this.LooseLeafOrSpecial) {
        if (this.isChapter) {
          renderText = translate('entity-title.chapter') + ' ' + this.number;
        } else {
          renderText = this.volumeTitle;
        }
      } else if (this.fallbackToVolume && this.isChapter && this.volumeTitle) {
        renderText = translate('entity-title.vol-num', {num: this.volumeTitle});
      } else if (this.fallbackToVolume && this.isChapter) { // this.volumeTitle === '' (this is a single volume on volume detail page)
        renderText = translate('entity-title.single-volume');
      } else {
        renderText = translate('entity-title.special');
      }
    }


    return renderText;
  }

  private calculateImageRenderText() {
    let renderText = '';

    if (this.number !== this.LooseLeafOrSpecial) {
      if (this.isChapter) {
        renderText = translate('entity-title.chapter') + ' ' + this.number;
      } else {
        renderText = this.volumeTitle;
      }
    } else {
      renderText = translate('entity-title.special');
    }

    return renderText;
  }


  private calculateComicRenderText() {
    let renderText = '';

    // If titleName is provided and prioritized
    if (this.titleName && this.prioritizeTitleName) {
      if (this.isChapter && this.includeChapter) {
        renderText = translate('entity-title.issue-num') + ' ' + this.number + ' - ';
      }
      renderText += this.titleName;
    } else {
      // Otherwise, check volume and number logic
      if (this.includeVolume && this.volumeTitle) {
        if (this.number !== this.LooseLeafOrSpecial) {
          renderText = this.isChapter ? this.volumeTitle : '';
        }
      }
      // Render either issue number or volume title, or "special" if applicable
      renderText += this.number !== this.LooseLeafOrSpecial
        ? (this.isChapter ? translate('entity-title.issue-num') + ' ' + this.number : this.volumeTitle)
        : translate('entity-title.special');
    }

    return renderText;
  }
}
