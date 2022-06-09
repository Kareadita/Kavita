import { Component, Input, OnInit } from '@angular/core';
import { Observable, of } from 'rxjs';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { HourEstimateRange } from 'src/app/_models/hour-estimate-range';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { AgeRating } from 'src/app/_models/metadata/age-rating';
import { Volume } from 'src/app/_models/volume';
import { MetadataService } from 'src/app/_services/metadata.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-entity-info-cards',
  templateUrl: './entity-info-cards.component.html',
  styleUrls: ['./entity-info-cards.component.scss']
})
export class EntityInfoCardsComponent implements OnInit {

  @Input() entity!: Volume | Chapter;
  /**
   * This will pull extra information 
   */
  @Input() includeMetadata: boolean = false;

  /**
   * Hide the Id field
   */
  @Input() hideID: boolean = false;

  isChapter = false;
  chapter!: Chapter;

  chapterMetadata!: ChapterMetadata;
  ageRating!: string;
  totalPages: number = 0;
  totalWordCount: number = 0;
  readingTime: HourEstimateRange = {maxHours: 1, minHours: 1, avgHours: 1, hasProgress: false};

  get LibraryType() {
    return LibraryType;
  }

  get MangaFormat() {
    return MangaFormat;
  }

  get AgeRating() {
    return AgeRating;
  }

  constructor(private utilityService: UtilityService, private seriesService: SeriesService, private metadataService: MetadataService, private readerService: ReaderService) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);

    this.chapter = this.utilityService.isChapter(this.entity) ? (this.entity as Chapter) : (this.entity as Volume).chapters[0];

    if (this.includeMetadata) {
      this.seriesService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
        this.chapterMetadata = metadata;
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
        if (this.chapter.timeEstimate) this.readingTime = this.chapter.timeEstimate;
        else this.readerService.getManualTimeToRead(this.chapter.wordCount, this.totalPages, this.chapter.files[0].format === MangaFormat.EPUB).subscribe((time) => this.readingTime = time);
      } else {
        const est = this.utilityService.asVolume(this.entity).timeEstimate;
        if (est) this.readingTime = est;
        else {
          this.readerService.getManualTimeToRead(this.totalWordCount, this.totalPages, this.chapter.files[0].format === MangaFormat.EPUB).subscribe((time) => this.readingTime = time);
        }
      }
  }

}
