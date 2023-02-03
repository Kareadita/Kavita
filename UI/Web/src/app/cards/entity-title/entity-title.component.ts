import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { LibraryType } from 'src/app/_models/library';
import { Volume } from 'src/app/_models/volume';

@Component({
  selector: 'app-entity-title',
  templateUrl: './entity-title.component.html',
  styleUrls: ['./entity-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityTitleComponent implements OnInit {

  /**
   * Library type for which the entity belongs
   */
  @Input() libraryType: LibraryType = LibraryType.Manga;
  @Input() seriesName: string = '';
  @Input() entity!: Volume | Chapter;
  /**
   * When generating the title, should this prepend 'Volume number' before the Chapter wording
   */
  @Input() includeVolume: boolean = false;
  /**
   * When a titleName (aka a title) is avaliable on the entity, show it over Volume X Chapter Y
   */
  @Input() prioritizeTitleName: boolean = true;

  isChapter = false;
  titleName: string = '';
  volumeTitle: string = '';


  get LibraryType() {
    return LibraryType;
  }

  constructor(private utilityService: UtilityService, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.isChapter = this.utilityService.isChapter(this.entity);

    if (this.isChapter) {
      const c = (this.entity as Chapter);
      this.volumeTitle = c.volumeTitle || '';
      this.titleName = c.titleName || '';
    } else {
      const v = this.utilityService.asVolume(this.entity);
      this.volumeTitle = v.name || '';
      this.titleName = v.name || '';
      if (v.chapters[0].titleName) {
        this.titleName += ' - ' + v.chapters[0].titleName;
      }
    }
    this.cdRef.markForCheck();
  }
}
