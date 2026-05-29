import {inject, Injectable} from '@angular/core';
import {map, Observable, of} from "rxjs";
import {UploadService} from "./upload.service";
import {ImageService} from "./image.service";
import {Series} from "../_models/series";
import {Chapter, LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../_models/chapter";
import {Volume} from "../_models/volume";
import {EntityTitleService} from "./entity-title.service";
import {Library, LibraryType} from "../_models/library/library";
import {UserCollection} from "../_models/collection-tag";
import {ReadingList} from "../_models/reading-list/reading-list";
import {Person} from "../_models/metadata/person";
import {LicenseService} from "./license.service";
import {ReadingListService} from "./reading-list.service";

export interface CoverImageOption {
  /** Image URL used to render the preview (remote URL, cover-upload URL, or data URL). */
  url: string;
  title: string;
  subtitle?: string;
  /** Filename of the image once staged in the temp directory. Populated lazily on selection. */
  fileName?: string;
}

export interface CoverImageChooserConfig {
  isLocked?: boolean | null;
  resetFunc?: () => Observable<unknown>;
  selected?: CoverImageOption;
  volumeFunc?: Observable<CoverImageOption[]>;
  chapterFunc?: Observable<CoverImageOption[]>;
  kavitaplusFunc?: Observable<CoverImageOption[]>;
  otherFunc?: Observable<CoverImageOption[]>;
}

@Injectable({
  providedIn: 'root',
})
export class CoverChooserConfigFactoryService {

  private readonly uploadService = inject(UploadService);
  private readonly imageService = inject(ImageService);
  private readonly entityTitleService = inject(EntityTitleService);
  private readonly licenseService = inject(LicenseService);
  private readonly readinglistService = inject(ReadingListService);


  public forSeries(series: Series, volumes: Volume[], libraryType: LibraryType) {
    const looseLeafChapterVolume = volumes.filter((v: Volume) => v.minNumber === LooseLeafOrDefaultNumber || v.minNumber === SpecialVolumeNumber);

    let looseLeafChapters: Observable<CoverImageOption[]> | undefined = undefined;
    if (looseLeafChapterVolume.length > 0) {
      const opts = looseLeafChapterVolume.flatMap(v =>
        v.chapters.map((c: Chapter) => ({
          url: this.imageService.getChapterCoverImage(c.id),
          title: this.entityTitleService.computeTitle(c, libraryType, { prioritizeTitleName: false, includeVolume: false })
        } as CoverImageOption))
      );
      looseLeafChapters = of(opts);
    }

    const nonLooseLeafChapterVolumes: Volume[] = volumes.filter((v: Volume) => v.minNumber !== LooseLeafOrDefaultNumber && v.minNumber !== SpecialVolumeNumber);

    return {
      isLocked: series.coverImageLocked,
      resetFunc: () => this.uploadService.updateSeriesCoverImage(series.id, '', false),
      selected: { url: this.imageService.getSeriesCoverImage(series.id), title: series.name },
      volumeFunc: nonLooseLeafChapterVolumes.length > 0
        ? of(nonLooseLeafChapterVolumes.map(v => ({ url: this.imageService.getVolumeCoverImage(v.id),
          title: this.entityTitleService.computeTitle(v, libraryType, { prioritizeTitleName: false, includeVolume: true }) } as CoverImageOption)))
        : undefined,
      chapterFunc: looseLeafChapters,
      kavitaplusFunc: this.licenseService.hasActiveLicense() ?
        this.imageService.getKavitaPlusSeriesCoverImages(series.id) : undefined
      ,
    };
  }

  public forVolume(volume: Volume, libraryType: LibraryType): CoverImageChooserConfig {
    return {
      isLocked: volume.coverImageLocked,
      resetFunc: () => this.uploadService.updateVolumeCoverImage(volume.id, '', false),
      selected: {
        url: this.imageService.getVolumeCoverImage(volume.id),
        title: this.entityTitleService.computeTitle(volume, libraryType, { prioritizeTitleName: false, includeVolume: true, fallbackToVolume: true })
      },
      kavitaplusFunc: this.licenseService.hasActiveLicense() ?
        this.imageService.getKavitaPlusSeriesCoverImages(volume.seriesId, volume.id) : undefined
    };
  }

  public forChapter(chapter: Chapter, libraryType: LibraryType, seriesId: number): CoverImageChooserConfig {
    return {
      isLocked: chapter.coverImageLocked,
      resetFunc: () => this.uploadService.updateChapterCoverImage(chapter.id, '', false),
      selected: { url: this.imageService.getChapterCoverImage(chapter.id), title: this.entityTitleService.computeTitle(chapter, libraryType) },
      kavitaplusFunc: this.licenseService.hasActiveLicense() ?
        this.imageService.getKavitaPlusSeriesCoverImages(seriesId, null, chapter.id) : undefined
    };
  }

  public forCollection(tag: UserCollection, series: Series[]): CoverImageChooserConfig {
    return {
      isLocked: tag.coverImageLocked,
      resetFunc: () => this.uploadService.updateCollectionCoverImage(tag.id, '', false),
      selected: { url: this.imageService.getCollectionCoverImage(tag.id), title: tag.title },
      otherFunc: of(series.map(s => {
        return {url: this.imageService.getSeriesCoverImage(s.id), title: s.name}
      }) as CoverImageOption[])
    };
  }

  public forReadingList(list: ReadingList): CoverImageChooserConfig {
    return {
      isLocked: list.coverImageLocked || list.coverImage != null,
      resetFunc: () => this.uploadService.updateReadingListCoverImage(list.id, '', false),
      selected: { url: this.imageService.getReadingListCoverImage(list.id), title: list.title },
      chapterFunc: this.readinglistService.getListItems(list.id).pipe(map(items => {
        return items.map(i => {
          return {
            url: this.imageService.getChapterCoverImage(i.chapterId),
            title: i.title
          }
        })
      }))
    };
  }

  public forPerson(person: Person): CoverImageChooserConfig {
    return {
      isLocked: person.coverImageLocked,
      resetFunc: () => this.uploadService.updatePersonCoverImage(person.id, '', false),
      selected: { url: this.imageService.getPersonImage(person.id), title: person.name },
    };
  }

  public forLibrary(library: Library | undefined): CoverImageChooserConfig {
    return {
      isLocked: library?.coverImage != null,
      resetFunc: library ? () => this.uploadService.updateLibraryCoverImage(library.id, '', false) : undefined,
      selected: (library?.coverImage != null && library?.coverImage !== '')
        ? { url: this.imageService.getLibraryCoverImage(library!.id), title: library!.name }
        : undefined,
    };
  }
}
