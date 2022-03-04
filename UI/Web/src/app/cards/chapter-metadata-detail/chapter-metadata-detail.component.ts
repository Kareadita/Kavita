import { Component, Input, OnInit } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { Chapter } from 'src/app/_models/chapter';
import { ChapterMetadata } from 'src/app/_models/chapter-metadata';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { LibraryType } from 'src/app/_models/library';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { PersonRole } from 'src/app/_models/person';

@Component({
  selector: 'app-chapter-metadata-detail',
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss']
})
export class ChapterMetadataDetailComponent implements OnInit {

  @Input() chapter!: ChapterMetadata;
  @Input() libraryType: LibraryType = LibraryType.Manga;

  roles: string[] = [];

  get LibraryType(): typeof LibraryType {
    return LibraryType;
  }

  constructor(private metadataService: MetadataService, public utilityService: UtilityService) { }

  ngOnInit(): void {
    this.roles = Object.keys(PersonRole).filter(role => /[0-9]/.test(role) === false);
  }

  getPeople(role: string) {
    if (this.chapter) {
      return (this.chapter as any)[role.toLowerCase()];
    }
    return [];
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
