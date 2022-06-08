import { Component, Input, OnInit } from '@angular/core';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { Volume } from 'src/app/_models/volume';

@Component({
  selector: 'app-entity-title',
  templateUrl: './entity-title.component.html',
  styleUrls: ['./entity-title.component.scss']
})
export class EntityTitleComponent implements OnInit {

  /**
   * Library type for which the entity belongs
   */
  @Input() libraryType: LibraryType = LibraryType.Manga;
  @Input() seriesName: string = '';
  @Input() entity!: Volume | Chapter;

  isChapter = false;
  chapter!: Chapter;

  get LibraryType() {
    return LibraryType;
  }

  constructor(private utilityService: UtilityService) { }

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);

    this.chapter = this.utilityService.isChapter(this.entity) ? (this.entity as Chapter) : (this.entity as Volume).chapters[0];
    console.log('seriesName: ', this.seriesName);
  }

}
