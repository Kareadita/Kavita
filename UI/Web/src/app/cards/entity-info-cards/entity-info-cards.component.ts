import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Input,
  OnInit,
  inject,
} from '@angular/core';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/metadata/chapter-metadata';
import { HourEstimateRange } from 'src/app/_models/series-detail/hour-estimate-range';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { Volume } from 'src/app/_models/volume';
import { SeriesService } from 'src/app/_services/series.service';
import { ImageService } from 'src/app/_services/image.service';
import {CommonModule} from "@angular/common";
import {IconAndTitleComponent} from "../../shared/icon-and-title/icon-and-title.component";
import {SafeHtmlPipe} from "../../pipe/safe-html.pipe";
import {DefaultDatePipe} from "../../pipe/default-date.pipe";
import {BytesPipe} from "../../pipe/bytes.pipe";
import {CompactNumberPipe} from "../../pipe/compact-number.pipe";
import {AgeRatingPipe} from "../../pipe/age-rating.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-entity-info-cards',
  standalone: true,
  imports: [CommonModule, IconAndTitleComponent, SafeHtmlPipe, DefaultDatePipe, BytesPipe, CompactNumberPipe, AgeRatingPipe, NgbTooltip],
  templateUrl: './entity-info-cards.component.html',
  styleUrls: ['./entity-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityInfoCardsComponent implements OnInit {

  @Input({required: true}) entity!: Volume | Chapter;
  /**
   * This will pull extra information
   */
  @Input() includeMetadata: boolean = false;

  /**
   * Hide more system based fields, like Id or Date Added
   */
  @Input() showExtendedProperties: boolean = true;

  isChapter = false;
  chapter!: Chapter;

  chapterMetadata!: ChapterMetadata;
  ageRating!: string;
  totalPages: number = 0;
  totalWordCount: number = 0;
  readingTime: HourEstimateRange = {maxHours: 1, minHours: 1, avgHours: 1};
  size: number = 0;

  imageService = inject(ImageService);

  get LibraryType() {
    return LibraryType;
  }

  get MangaFormat() {
    return MangaFormat;
  }

  get AgeRating() {
    return AgeRating;
  }

  get WebLinks() {
    if (this.chapter.webLinks === '') return [];
    return this.chapter.webLinks.split(',');
  }

  constructor(private utilityService: UtilityService, private seriesService: SeriesService, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);

    this.chapter = this.utilityService.isChapter(this.entity) ? (this.entity as Chapter) : (this.entity as Volume).chapters[0];


    if (this.isChapter) {
      this.size = this.utilityService.asChapter(this.entity).files.reduce((sum, v) => sum + v.bytes, 0);
    } else {
      this.size = this.utilityService.asVolume(this.entity).chapters.reduce((sum1, chapter) => {
        return sum1 + chapter.files.reduce((sum2, file) => {
          return sum2 + file.bytes;
        }, 0);
      }, 0);
    }

    if (this.includeMetadata) {
      this.seriesService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
        this.chapterMetadata = metadata;
        this.cdRef.markForCheck();
      });
    }

    this.totalPages = this.chapter.pages;
    if (!this.isChapter) {
      this.totalPages = this.utilityService.asVolume(this.entity).pages;
    }

    this.totalWordCount = this.chapter.wordCount;
    if (!this.isChapter) {
      this.totalWordCount = this.utilityService.asVolume(this.entity).chapters.map(c => c.wordCount).reduce((sum, d) => sum + d);
    }



    if (this.isChapter) {
      this.readingTime.minHours = this.chapter.minHoursToRead;
      this.readingTime.maxHours = this.chapter.maxHoursToRead;
      this.readingTime.avgHours = this.chapter.avgHoursToRead;
    } else {
      const vol = this.utilityService.asVolume(this.entity);
      this.readingTime.minHours = vol.minHoursToRead;
      this.readingTime.maxHours = vol.maxHoursToRead;
      this.readingTime.avgHours = vol.avgHoursToRead;
    }
    this.cdRef.markForCheck();
  }
}
