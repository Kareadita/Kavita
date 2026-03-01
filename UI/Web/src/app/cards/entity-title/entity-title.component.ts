import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {Chapter, LooseLeafOrDefaultNumber} from 'src/app/_models/chapter';
import {LibraryType} from 'src/app/_models/library/library';
import {Volume} from 'src/app/_models/volume';
import {TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {EntityTitleService} from "../../_services/entity-title.service";

/**
 * This is primarily used for list item
 */
@Component({
  selector: 'app-entity-title',
  imports: [
      TranslocoModule,
      DefaultValuePipe
  ],
  templateUrl: './entity-title.component.html',
  styleUrls: ['./entity-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EntityTitleComponent {

  private readonly utilityService = inject(UtilityService);
  private readonly entityTitleService = inject(EntityTitleService);

  protected readonly LooseLeafOrSpecial = LooseLeafOrDefaultNumber + "";
  protected readonly LibraryType = LibraryType;

  /**
   * Library type for which the entity belongs
   */
  libraryType = input.required<LibraryType>();
  entity = input.required<Volume | Chapter>();
  /**
   * When generating the title, should this prepend 'Volume number' before the Chapter wording
   */
  includeVolume = input<boolean>(false);
  /**
   * When generating the title, should this prepend 'Chapter number' before the Chapter titlename
   */
  includeChapter = input<boolean>(false);
  /**
   * When a titleName (aka a title) is available on the entity, show it over Volume X Chapter Y
   */
  prioritizeTitleName = input<boolean>(true);
  /**
   * When there is no meaningful title to display and the chapter is just a single volume, show the volume number
   */
  fallbackToVolume = input<boolean>(true);

  isChapter = computed(() => this.utilityService.isChapter(this.entity()));

  titleName = computed(() => {
    const isChapter = this.isChapter();
    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.titleName || '';
    }

    const volume = this.entity() as Volume;
    let title = volume.name || '';
    if (volume.chapters.length > 0 && volume.chapters[0].titleName) {
      title += ' - ' + volume.chapters[0].titleName;
    }

    return title;
  });
  volumeTitle = computed(() => {
    const isChapter = this.isChapter();

    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.volumeTitle || '';
    }

    const volume = this.entity() as Volume;
    return volume.name || '';
  });

  number = computed(() => {
    const isChapter = this.isChapter();

    if (isChapter) {
      const chapter = this.entity() as Chapter;
      return chapter.range || '';
    }

    const volume = this.entity() as Volume;
    return volume.name || '';
  });

  renderText = computed(() => this.entityTitleService.computeTitle(
    this.entity(), this.libraryType(), {
      prioritizeTitleName: this.prioritizeTitleName(),
      fallbackToVolume: this.fallbackToVolume(),
      includeChapter: this.includeChapter(),
      includeVolume: this.includeVolume(),
    }
  ));
}
