import {inject, Injectable, TemplateRef} from "@angular/core";
import {ImageService} from "./image.service";
import {ReaderService} from "./reader.service";
import {ActionableEntity, ActionFactoryService, ActionItem} from "./action-factory.service";
import {DownloadService} from "../shared/_services/download.service";
import {Router} from "@angular/router";
import {RelationshipPipe} from "../_pipes/relationship.pipe";
import {Series} from "../_models/series";
import {CardEntity, ChapterCardEntity, SeriesCardEntity} from "../_models/card/card-entity";
import {CardConfiguration, CardConfigurationOverrides} from "../_models/card/card-configuration";
import {Chapter, LooseLeafOrDefaultNumber} from "../_models/chapter";
import {map} from "rxjs/operators";
import {Volume} from "../_models/volume";
import {UserCollection} from "../_models/collection-tag";
import {ReadingList} from "../_models/reading-list";
import {LibraryType} from "../_models/library/library";
import {MangaFormat} from "../_models/manga-format";

/**
 * Factory service that creates CardConfiguration objects for each entity type.
 * Provides sensible defaults that can be overridden at call sites.
 *
 * Usage:
 *   // In component
 *   private configFactory = inject(CardConfigFactory);
 *
 *   config = computed(() => this.configFactory.forSeries({
 *     allowSelection: true,
 *     actionables: this.customActions
 *   }));
 */
@Injectable({ providedIn: 'root' })
export class CardConfigFactory {
  private readonly imageService = inject(ImageService);
  private readonly readerService = inject(ReaderService);
  private readonly actionFactory = inject(ActionFactoryService);
  private readonly downloadService = inject(DownloadService);
  private readonly router = inject(Router);
  private readonly relationshipPipe = new RelationshipPipe();

  /**
   * Creates configuration for Series cards
   */
  forSeries(
    actionCallback: (action: ActionItem<Series>, series: Series) => void,
    overrides?: CardConfigurationOverrides<Series>
  ): CardConfiguration<Series> {
    const defaults: CardConfiguration<Series> = {
      allowSelection: false,
      selectionType: 'series',
      suppressArchiveWarning: false,

      coverFunc: (s) => this.imageService.getSeriesCoverImage(s.id),
      titleFunc: (s) => s.name,
      titleRouteFunc: (s) => `/library/${s.libraryId}/series/${s.id}`,
      metaTitleFunc: (s, wrapper) => {
        const seriesWrapper = wrapper as SeriesCardEntity;
        if (seriesWrapper.relation) {
          return this.relationshipPipe.transform(seriesWrapper.relation);
        }
        return s.localizedName || s.name;
      },
      tooltipFunc: (s) => s.name,
      progressFunc: (s) => ({ pages: s.pages, pagesRead: s.pagesRead }),

      formatBadgeFunc: (s) => s.format,
      countFunc: () => 0,
      showErrorFunc: (s) => s.pages === 0,
      ariaLabelFunc: (s) => s.name,

      actionables: this.actionFactory.getSeriesActions(actionCallback),
      readFunc: (s) => this.readerService.readSeries(s, false),
      clickFunc: (s) => this.router.navigate(['library', s.libraryId, 'series', s.id]),

      downloadObservableFunc: (s) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, s))
      )
    };

    return this.mergeConfig(defaults, overrides);
  }

  forBookmark(
    actionCallback: (action: ActionItem<Series>, series: Series) => void,
    overrides?: CardConfigurationOverrides<Series>
  ): CardConfiguration<Series> {
    const defaults: CardConfiguration<Series> = {
      allowSelection: true,
      selectionType: 'bookmark',
      suppressArchiveWarning: true,

      coverFunc: (s) => this.imageService.getSeriesCoverImage(s.id),
      titleFunc: (s) => s.name,
      titleRouteFunc: (s) => `/library/${s.libraryId}/series/${s.id}`,
      metaTitleFunc: (s, wrapper) => s.name,
      tooltipFunc: (s) => s.name,
      progressFunc: (s) => ({ pages: s.pages, pagesRead: s.pagesRead }),

      formatBadgeFunc: (s) => s.format,
      countFunc: () => 0,
      showErrorFunc: (s) => false,
      ariaLabelFunc: (s) => s.name,

      actionables: this.actionFactory.getBookmarkActions(actionCallback),
      readFunc: (s) => this.router.navigate(['library', s.libraryId, 'series', s.id, 'manga', 0], {queryParams: {incognitoMode: false, bookmarkMode: true}}),
      clickFunc: (s) => this.router.navigate(['library', s.libraryId, 'series', s.id, 'manga', 0], {queryParams: {incognitoMode: false, bookmarkMode: true}}),

      downloadObservableFunc: (s) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, s))
      )
    };

    return this.mergeConfig(defaults, overrides);
  }


  /**
   * Creates configuration for Chapter cards
   */
  forChapter(
    seriesId: number, libraryId: number, libraryType: LibraryType, actionCallback: (action: ActionItem<Chapter>, chapter: Chapter) => void, overrides?: CardConfigurationOverrides<Chapter>  ): CardConfiguration<Chapter> {
    const defaults: CardConfiguration<Chapter> = {
      allowSelection: false,
      selectionType: 'chapter',
      suppressArchiveWarning: false,

      coverFunc: (c) => this.imageService.getChapterCoverImage(c.id),
      titleFunc: (c) => c.titleName || c.title || c.range,
      titleRouteFunc: (c) => `/library/${libraryId}/series/${seriesId}/chapter/${c.id}`,
      metaTitleFunc: (c, wrapper) => {
        if (c.isSpecial) {
          return c.title || c.range;
        }
        return c.titleName || '';
      },
      tooltipFunc: (c) => c.titleName || c.title || c.range,
      progressFunc: (c) => ({ pages: c.pages, pagesRead: c.pagesRead }),

      formatBadgeFunc: () => null,
      countFunc: (c) => c.files?.length > 1 && c.files[0].format !== MangaFormat.IMAGE ? c.files.length : 0,
      showErrorFunc: (c) => {
        const wrapper = overrides as unknown as ChapterCardEntity;
        return c.pages === 0 && !wrapper?.suppressArchiveWarning;
      },
      ariaLabelFunc: (c) => c.titleName || c.title || c.range,

      actionables: this.actionFactory.getChapterActions(actionCallback),
      readFunc: (c) => this.readerService.readChapter(libraryId, seriesId, c, false),
      clickFunc: (c) => this.router.navigate(['library', libraryId, 'series', seriesId, 'chapter', c.id]),

      downloadObservableFunc: (c) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, c))
      )
    };

    return this.mergeConfig(defaults, overrides);
  }

  /**
   * Creates configuration for Volume cards
   */
  forVolume(
    seriesId: number,
    libraryId: number,
    libraryType: LibraryType,
    actionCallback: (action: ActionItem<Volume>, volume: Volume) => void,
    overrides?: CardConfigurationOverrides<Volume>
  ): CardConfiguration<Volume> {
    const defaults: CardConfiguration<Volume> = {
      allowSelection: false,
      selectionType: 'volume',
      suppressArchiveWarning: false,

      coverFunc: (v) => this.imageService.getVolumeCoverImage(v.id),
      titleFunc: (v) => v.name,
      titleRouteFunc: (v) => `/library/${libraryId}/series/${seriesId}/volume/${v.id}`,
      metaTitleFunc: (v) => {
        if (libraryType === LibraryType.Images) return '';
        if ([LibraryType.LightNovel || LibraryType.Book].includes(libraryType)) {
          return v.name;
        }
        if (v.hasOwnProperty('chapters') && v.chapters.length > 0 && v.chapters[0].titleName) {
          v.chapters[0].titleName
        }

        return v.name;
      },
      tooltipFunc: (v) => v.name,
      progressFunc: (v) => ({ pages: v.pages, pagesRead: v.pagesRead }),

      formatBadgeFunc: () => null,
      // Show file count if there are duplicate files for volume, not just chapter count
      countFunc: (v) => (v?.chapters || [])
        .filter(c => c.minNumber === LooseLeafOrDefaultNumber)
        .flatMap(c => c.files)
        .length,
      showErrorFunc: (v) => v.pages === 0,
      ariaLabelFunc: (v) => v.name,

      actionables: this.actionFactory.getVolumeActions(actionCallback),
      readFunc: (v) => {
        this.readerService.readVolume(libraryId, seriesId, v, false);
      },

      downloadObservableFunc: (v) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, v))
      )
    };

    return this.mergeConfig(defaults, overrides);
  }

  /**
   * Creates configuration for Collection cards
   */
  forCollection(
    actionables: ActionItem<UserCollection>[],
    templateRef: TemplateRef<{ $implicit: CardEntity }> | undefined,
    overrides?: CardConfigurationOverrides<UserCollection>
  ): CardConfiguration<UserCollection> {
    const defaults: CardConfiguration<UserCollection> = {
      allowSelection: false,
      selectionType: 'collection',
      suppressArchiveWarning: true,

      coverFunc: (c) => this.imageService.getCollectionCoverImage(c.id),
      titleFunc: (c) => c.title,
      titleRouteFunc: (c) => `/collections/${c.id}`,
      metaTitleFunc: (c) => '',
      metaTitleTemplate: templateRef,
      tooltipFunc: (c) => c.title,
      progressFunc: () => ({ pages: 0, pagesRead: 0 }),

      formatBadgeFunc: () => null,
      countFunc: (c) => c.itemCount,
      showErrorFunc: () => false,
      ariaLabelFunc: (c) => c.title,

      actionables: actionables,
      readFunc: () => {},
      clickFunc: (c) => this.router.navigate(['collections', c.id]),
    };

    return this.mergeConfig(defaults, overrides);
  }

  /**
   * Creates configuration for ReadingList cards
   */
  forReadingList(
    actionCallback: (action: ActionItem<ReadingList>, list: ReadingList) => void,
    overrides?: CardConfigurationOverrides<ReadingList>
  ): CardConfiguration<ReadingList> {
    const defaults: CardConfiguration<ReadingList> = {
      allowSelection: false,
      selectionType: 'readingList',
      suppressArchiveWarning: true,

      coverFunc: (r) => this.imageService.getReadingListCoverImage(r.id),
      titleFunc: (r) => r.title,
      titleRouteFunc: (r) => `/lists/${r.id}`,
      metaTitleFunc: (r) => r.summary || '',
      tooltipFunc: (r) => r.title,
      progressFunc: () => ({ pages: 0, pagesRead: 0 }),

      formatBadgeFunc: () => null,
      countFunc: () => 0,
      showErrorFunc: () => false,
      ariaLabelFunc: (r) => r.title,

      actionables: this.actionFactory.getReadingListActions(actionCallback),
      readFunc: () => {},

      downloadObservableFunc: () => null!
    };

    return this.mergeConfig(defaults, overrides);
  }

  /**
   * Merges default configuration with overrides.
   * Overrides take precedence.
   */
  private mergeConfig<T extends ActionableEntity>(
    defaults: CardConfiguration<T>,
    overrides?: CardConfigurationOverrides<T>
  ): CardConfiguration<T> {
    if (!overrides) return defaults;
    return { ...defaults, ...overrides };
  }
}
