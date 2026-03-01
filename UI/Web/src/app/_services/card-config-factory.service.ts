import {inject, Injectable, TemplateRef} from "@angular/core";
import {ImageService} from "./image.service";
import {ReaderService} from "./reader.service";
import {ActionableEntity, ActionFactoryService} from "./action-factory.service";
import {DownloadService} from "../shared/_services/download.service";
import {Router} from "@angular/router";
import {RelationshipPipe} from "../_pipes/relationship.pipe";
import {Series} from "../_models/series";
import {CardEntity, ChapterCardEntity, RelatedSeriesCardEntity, SeriesCardEntity} from "../_models/card/card-entity";
import {EntityTitleService} from "./entity-title.service";
import {
  ActionableCardConfiguration,
  BaseCardConfiguration,
  BaseCardConfigurationOverrides,
  CardConfigurationOverrides
} from "../_models/card/card-configuration";
import {Chapter, LooseLeafOrDefaultNumber} from "../_models/chapter";
import {map} from "rxjs/operators";
import {Volume} from "../_models/volume";
import {UserCollection} from "../_models/collection-tag";
import {ReadingList} from "../_models/reading-list";
import {LibraryType} from "../_models/library/library";
import {MangaFormat} from "../_models/manga-format";
import {User} from "../_models/user/user";
import {PageBookmark} from "../_models/readers/page-bookmark";
import {RelatedSeriesPair} from "../_single-module/related-tab/related-tab.component";
import {ActionItem} from "../_models/actionables/action-item";
import {SeriesGroup} from "../_models/series-group";

export interface ConfigCardFactoryBaseParameters<T> {
  shouldRenderAction?: (action: ActionItem<T>, entity: T, user: User) => boolean,
  titleRef?: TemplateRef<{ $implicit: CardEntity }> | undefined,
  metaTitleRef?: TemplateRef<{ $implicit: CardEntity }> | undefined,
  overrides?: BaseCardConfigurationOverrides<T>,
}

export interface ConfigCardFactoryActionableParameters<T extends ActionableEntity> {
  shouldRenderAction?: (action: ActionItem<T>, entity: T, user: User) => boolean,
  titleRef?: TemplateRef<{ $implicit: CardEntity }> | undefined,
  metaTitleRef?: TemplateRef<{ $implicit: CardEntity }> | undefined,
  overrides?: CardConfigurationOverrides<T>,
}

export interface ConfigCardFactoryChapterVolumeParameters<T extends ActionableEntity> extends ConfigCardFactoryActionableParameters<T> {
  seriesId: number;
  libraryId: number;
  libraryType: number;
}


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
  private readonly entityTitleService = inject(EntityTitleService);

  /**
   * Creates configuration for Series cards
   */
  forSeries(
    params?: ConfigCardFactoryActionableParameters<Series>
  ): ActionableCardConfiguration<Series> {
    const defaults: ActionableCardConfiguration<Series> = {
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
      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,
      tooltipFunc: (s) => s.name,
      progressFunc: (s) => ({ pages: s.pages, pagesRead: s.pagesRead }),

      formatBadgeFunc: (s) => s.format,
      countFunc: () => 0,
      showErrorFunc: (s) => s.pages === 0,
      ariaLabelFunc: (s) => s.name,

      actionableFunc: (s) => this.actionFactory.getSeriesActions(),
      readFunc: (s) => this.readerService.readSeries(s, false),
      clickFunc: (s) => this.router.navigate(['library', s.libraryId, 'series', s.id]),

      downloadObservableFunc: (s) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, s))
      ),

      progressUpdateStrategy: {
        getMatchCriteria: (s) => ({ seriesId: s.id }),
        // Series cards don't contain chapter/volume details
        // Signal that parent needs to refetch
        applyUpdate: () => null
      }
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  forRelationship(
    params?: ConfigCardFactoryBaseParameters<RelatedSeriesPair>
  ): BaseCardConfiguration<RelatedSeriesPair> {
    const defaults: BaseCardConfiguration<RelatedSeriesPair> = {
      allowSelection: false,
      selectionType: 'series',
      suppressArchiveWarning: false,

      coverFunc: (s) => this.imageService.getSeriesCoverImage(s.series.id),
      titleFunc: (s) => s.series.name,
      titleRouteFunc: (s) => `/library/${s.series.libraryId}/series/${s.series.id}`,
      metaTitleFunc: (s, wrapper) => {
        const seriesWrapper = wrapper as RelatedSeriesCardEntity;
        if (seriesWrapper.data.relation) {
          return this.relationshipPipe.transform(seriesWrapper.data.relation);
        }
        return s.series.localizedName || s.series.name;
      },
      tooltipFunc: (s) => s.series.name,
      progressFunc: (s) => ({ pages: s.series.pages, pagesRead: s.series.pagesRead }),

      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: (s) => s.series.format,
      countFunc: () => 0,
      showErrorFunc: (s) => s.series.pages === 0,
      ariaLabelFunc: (s) => s.series.name,

      readFunc: (s) => this.readerService.readSeries(s.series, false),
      clickFunc: (s) => this.router.navigate(['library', s.series.libraryId, 'series', s.series.id]),

      downloadObservableFunc: (s) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, s.series))
      )
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  forBookmark(
    params?: ConfigCardFactoryActionableParameters<PageBookmark>
  ): ActionableCardConfiguration<PageBookmark> {
    const defaults: ActionableCardConfiguration<PageBookmark> = {
      allowSelection: true,
      selectionType: 'bookmark',
      suppressArchiveWarning: true,

      coverFunc: (s) => this.imageService.getSeriesCoverImage(s.series!.id),
      titleFunc: (s) => s.series!.name,
      titleRouteFunc: (s) => `/library/${s.series!.libraryId}/series/${s.seriesId}}`,
      metaTitleFunc: (s, wrapper) => s.series!.name,
      tooltipFunc: (s) => s.series!.name,
      progressFunc: (s) => ({ pages: s.series!.pages, pagesRead: s.series!.pagesRead }),

      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: (s) => s.series!.format,
      countFunc: () => 0,
      showErrorFunc: (s) => false,
      ariaLabelFunc: (s) => s.series!.name,
      titleRouteParamsFunc: (s) => {return { bookmarkMode: true }},

      actionableFunc: (s) => this.actionFactory.getBookmarkActions(() => ({seriesId: s.series!.id, libraryId: s.series!.libraryId, seriesName: s.series!.name}), params?.shouldRenderAction),

      readFunc: (s) => this.router.navigate(['library', s.series!.libraryId, 'series', s.seriesId, 'manga', s.chapterId], {queryParams: {incognitoMode: false, bookmarkMode: true}}),
      clickFunc: (s) => this.router.navigate(['library', s.series!.libraryId, 'series', s.seriesId, 'manga', s.chapterId], {queryParams: {incognitoMode: false, bookmarkMode: true}}),

      downloadObservableFunc: (s) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, s))
      )
    };

    return this.mergeConfig(defaults, params?.overrides);
  }


  /**
   * Creates configuration for Chapter cards
   */
  forChapter(params: ConfigCardFactoryChapterVolumeParameters<Chapter>): ActionableCardConfiguration<Chapter> {
    const defaults: ActionableCardConfiguration<Chapter> = {
      allowSelection: false,
      selectionType: 'chapter',
      suppressArchiveWarning: false,

      coverFunc: (c) => this.imageService.getChapterCoverImage(c.id),
      titleFunc: (c) => this.entityTitleService.computeTitle(c, params.libraryType, { prioritizeTitleName: false }),
      titleRouteFunc: (c) => `/library/${params.libraryId}/series/${params.seriesId}/chapter/${c.id}`,
      metaTitleFunc: (c, wrapper) => {
        if (c.isSpecial) {
          return c.title || c.range;
        }
        return c.titleName || '';
      },
      tooltipFunc: (c) => c.titleName || c.title || (c.range === (LooseLeafOrDefaultNumber + '') ? '' : c.range),
      progressFunc: (c) => ({ pages: c.pages, pagesRead: c.pagesRead }),
      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: () => null,
      countFunc: (c) => c.files?.length > 1 && c.files[0].format !== MangaFormat.IMAGE ? c.files.length : 0,
      showErrorFunc: (c) => {
        const wrapper = params?.overrides as unknown as ChapterCardEntity;
        return c.pages === 0 && !wrapper?.suppressArchiveWarning;
      },
      ariaLabelFunc: (c) => c.titleName || c.title || (c.range === (LooseLeafOrDefaultNumber + '') ? '' : c.range),

      actionableFunc: (c) => this.actionFactory.getChapterActions(params.seriesId, params.libraryId, params.libraryType, params?.shouldRenderAction),
      readFunc: (c) => this.readerService.readChapter(params.libraryId, params.seriesId, c, false),
      clickFunc: (c) => this.router.navigate(['library', params.libraryId, 'series', params.seriesId, 'chapter', c.id]),

      downloadObservableFunc: (c) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, c))
      ),

      progressUpdateStrategy: {
        getMatchCriteria: (c) => ({ chapterId: c.id }),
        applyUpdate: (c, event) => ({
          ...c,
          pagesRead: event.pagesRead
        })
      }
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  /**
   * Creates configuration for Volume cards
   */
  forVolume(
    params: ConfigCardFactoryChapterVolumeParameters<Volume>
  ): ActionableCardConfiguration<Volume> {
    const defaults: ActionableCardConfiguration<Volume> = {
      allowSelection: true,
      selectionType: 'volume',
      suppressArchiveWarning: false,

      coverFunc: (v) => this.imageService.getVolumeCoverImage(v.id),
      titleFunc: (v) => v.name,
      titleRouteFunc: (v) => `/library/${params.libraryId}/series/${params.seriesId}/volume/${v.id}`,
      metaTitleFunc: (v) => {
        if (params.libraryType === LibraryType.Images) return '';
        if ([LibraryType.LightNovel || LibraryType.Book].includes(params.libraryType)) {
          return v.name;
        }
        if (v.hasOwnProperty('chapters') && v.chapters.length > 0 && v.chapters[0].titleName) {
          v.chapters[0].titleName
        }

        return v.name;
      },
      tooltipFunc: (v) => v.name,
      progressFunc: (v) => ({ pages: v.pages, pagesRead: v.pagesRead }),

      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: () => null,
      // Show file count if there are duplicate files for volume, not just chapter count
      countFunc: (v) => (v?.chapters || [])
        .filter(c => c.minNumber === LooseLeafOrDefaultNumber)
        .flatMap(c => c.files)
        .length,
      showErrorFunc: (v) => v.pages === 0,
      ariaLabelFunc: (v) => v.name,

      actionableFunc: (v) => this.actionFactory.getVolumeActions(params.seriesId, params.libraryId, params.libraryType, params?.shouldRenderAction),
      readFunc: (v) => {
        this.readerService.readVolume(params.libraryId, params.seriesId, v, false);
      },

      downloadObservableFunc: (v) => this.downloadService.activeDownloads$.pipe(
        map(events => this.downloadService.mapToEntityType(events, v))
      ),

      progressUpdateStrategy: {
        getMatchCriteria: (v) => ({volumeId: v.id}),
        applyUpdate: (v, event) => {
          // Find and update the specific chapter
          const chapterIndex = v.chapters.findIndex(c => c.id === event.chapterId);
          if (chapterIndex === -1) return v; // Chapter not in this volume

          const updatedChapters = [...v.chapters];
          updatedChapters[chapterIndex] = {
            ...updatedChapters[chapterIndex],
            pagesRead: event.pagesRead
          };

          return {
            ...v,
            chapters: updatedChapters,
            pagesRead: updatedChapters.reduce((sum, c) => sum + c.pagesRead, 0)
          };
        }
      }
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  /**
   * Creates configuration for Collection cards
   */
  forCollection(params?: ConfigCardFactoryActionableParameters<UserCollection>): ActionableCardConfiguration<UserCollection> {
    const defaults: ActionableCardConfiguration<UserCollection> = {
      allowSelection: true,
      selectionType: 'collection',
      suppressArchiveWarning: true,

      coverFunc: (c) => this.imageService.getCollectionCoverImage(c.id),
      titleFunc: (c) => c.title,
      titleRouteFunc: (c) => `/collections/${c.id}`,
      metaTitleFunc: (c) => '',
      tooltipFunc: (c) => c.title,
      progressFunc: () => ({ pages: 0, pagesRead: 0 }),

      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: () => null,
      countFunc: (c) => c.itemCount,
      showErrorFunc: () => false,
      ariaLabelFunc: (c) => c.title,

      actionableFunc: (c) => this.actionFactory.getCollectionTagActions(params?.shouldRenderAction),
      readFunc: null,
      clickFunc: (c) => this.router.navigate(['collections', c.id]),
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  /**
   * Creates configuration for ReadingList cards
   */
  forReadingList(params?: ConfigCardFactoryActionableParameters<ReadingList>): ActionableCardConfiguration<ReadingList> {
    const defaults: ActionableCardConfiguration<ReadingList> = {
      allowSelection: true,
      selectionType: 'readingList',
      suppressArchiveWarning: true,

      coverFunc: (r) => this.imageService.getReadingListCoverImage(r.id),
      titleFunc: (r) => r.title,
      titleRouteFunc: (r) => `/lists/${r.id}`,
      metaTitleFunc: (r) => r.summary || '',
      tooltipFunc: (r) => r.title,
      progressFunc: () => ({ pages: 0, pagesRead: 0 }),

      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,

      formatBadgeFunc: () => null,
      countFunc: (r) => r.itemCount,
      showErrorFunc: () => false,
      ariaLabelFunc: (r) => r.title,

      actionableFunc: (r) => this.actionFactory.getReadingListActions(params?.shouldRenderAction),
      readFunc: null,
      clickFunc: (r) => this.router.navigate(['lists', r.id]),
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  /**
   * Creates configuration for Recently cards
   */
  forRecentlyUpdated(
    params?: ConfigCardFactoryBaseParameters<SeriesGroup>
  ): BaseCardConfiguration<SeriesGroup> {
    const defaults: BaseCardConfiguration<SeriesGroup> = {
      allowSelection: false,
      selectionType: 'series',
      suppressArchiveWarning: false,

      coverFunc: (s) => this.imageService.getSeriesCoverImage(s.seriesId),
      titleFunc: (s) => s.seriesName,
      titleRouteFunc: (s) => `/library/${s.libraryId}/series/${s.seriesId}`,
      metaTitleFunc: (s, wrapper) => '',
      titleTemplate: params?.titleRef,
      metaTitleTemplate: params?.metaTitleRef,
      tooltipFunc: (s) => s.seriesName,
      progressFunc: (s) => ({ pages: 0, pagesRead: 0 }),

      formatBadgeFunc: (s) => s.format,
      countFunc: (s) => s.count,
      showErrorFunc: (s) => false,
      ariaLabelFunc: (s) => s.seriesName,

      readFunc: null,
      clickFunc: (s) => this.router.navigate(['library', s.libraryId, 'series', s.seriesId]),
    };

    return this.mergeConfig(defaults, params?.overrides);
  }

  /**
   * Merges default configuration with overrides.
   * Overrides take precedence.
   */
  private mergeConfig<C extends BaseCardConfiguration<any>>(
    defaults: C,
    overrides?: Partial<C>
  ): C {
    if (!overrides) return defaults;
    return { ...defaults, ...overrides } as C;
  }
}
