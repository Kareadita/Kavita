import { Component, Input, OnInit } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { LibraryType } from 'src/app/_models/library';
import { ActionItem } from 'src/app/_services/action-factory.service';

@Component({
  selector: 'app-chapter-metadata-detail',
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss']
})
export class ChapterMetadataDetailComponent implements OnInit {

  @Input() chapter!: Chapter;
  @Input() libraryType: LibraryType = LibraryType.Manga;
  //metadata!: ChapterMetadata;

  get LibraryType(): typeof LibraryType {
    return LibraryType;
  }

  constructor(private metadataService: MetadataService, public utilityService: UtilityService) { }

  ngOnInit(): void {
    // this.metadataService.getChapterMetadata(this.chapter.id).subscribe(metadata => {
    //   console.log('Chapter ', this.chapter.number, ' metadata: ', metadata);
    //   this.metadata = metadata;
    // })
  }

  performAction(action: ActionItem<Chapter>, chapter: Chapter) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, chapter);
    }
  }

  readChapter(chapter: Chapter) {
    // if (chapter.pages === 0) {
    //   this.toastr.error('There are no pages. Kavita was not able to read this archive.');
    //   return;
    // }

    // if (chapter.files.length > 0 && chapter.files[0].format === MangaFormat.EPUB) {
    //   this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'book', chapter.id]);
    // } else {
    //   this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'manga', chapter.id]);
    // }
  }

}
