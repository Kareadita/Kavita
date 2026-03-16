import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  OnChanges,
  output,
  SimpleChanges,
} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {Chapter} from "../../_models/chapter";
import {FormsModule} from "@angular/forms";
import {LibraryType} from "../../_models/library/library";
import {MangaFormat} from "../../_models/manga-format";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {BaseCardConfiguration, ProgressUpdateResult} from "../../_models/card/card-configuration";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {BulkSelectionEntityDataSource} from "../bulk-selection.service";
import {ActionFactoryService} from "../../_services/action-factory.service";

@Component({
  selector: 'app-chapter-card',
  imports: [
    FormsModule,
    EntityCardComponent
  ],
  templateUrl: './chapter-card.component.html',
  styleUrl: './chapter-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterCardComponent implements OnChanges {
  public readonly imageService = inject(ImageService);
  private readonly configFactory = inject(CardConfigFactory);
  private readonly actionFactory = inject(ActionFactoryService);


  protected readonly LibraryType = LibraryType;
  protected readonly MangaFormat = MangaFormat;

  libraryId = input.required<number>();
  seriesId = input.required<number>();
  chapter = input.required<Chapter>();
  libraryType = input.required<number>();

  index = input<number>(0);
  maxIndex = input<number>(1);
  dataSource = input<BulkSelectionEntityDataSource>('chapter');

  /**
   * Any actions to perform on the card
   */
  actions = computed(() => this.actionFactory.getChapterActions(this.seriesId(), this.libraryId(), this.libraryType()));
  /**
   * If the entity should show selection code
   */
  allowSelection = input<boolean>(false);
  /**
   * This will suppress the "cannot read archive warning" when total pages is 0
   */
  suppressArchiveWarning = input<boolean>(false);
  /**
   * When the card is selected.
   */
  selection = output<boolean>();
  /**
   * Emitted when the entity is deleted. Emits the entity id
   */
  reload = output<number>();
  /**
   * Underlying data has mutated, mutated data is returned
   */
  dataChanged = output<Chapter>();

  private chapterSignal = linkedSignal<Chapter>(() => this.chapter());

  cardEntity = computed<CardEntity>(() => {
    const chapter = this.chapterSignal();
    if (!chapter) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.chapter({} as Chapter, 0, 0);
    }
    return CardEntityFactory.chapter(chapter, this.seriesId(), this.libraryId());
  });

  config = computed<BaseCardConfiguration<Chapter>>(() => {
    return this.configFactory.forChapter({
      seriesId: this.seriesId(),
      libraryId: this.libraryId(),
      libraryType: this.libraryType(),
      overrides: {
        allowSelection: this.allowSelection(),
        actionableFunc: () => this.actions(),
        selectionType: this.dataSource(),
      }
    });
  });

  ngOnChanges(changes: SimpleChanges) {
    if (changes['chapter']) {
      this.chapterSignal.set(this.chapter());
    }
  }

  onDataChanged(entity: Chapter) {
    this.chapterSignal.set({...entity});
    this.dataChanged.emit(entity);
  }

  onProgressUpdated(result: ProgressUpdateResult<Chapter>) {
    if (result.requiresRefetch) {
      this.reload.emit(result.entity!.id);
      return;
    }

    this.onDataChanged(result.entity!);
  }
}
