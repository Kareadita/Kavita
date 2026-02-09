import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  Input,
  OnChanges,
  OnInit,
  Output,
  signal,
  SimpleChanges,
  TemplateRef,
  viewChild
} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FormsModule} from "@angular/forms";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {LibraryType} from "../../_models/library/library";
import {MangaFormat} from "../../_models/manga-format";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {BaseCardConfiguration, ProgressUpdateResult} from "../../_models/card/card-configuration";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {BulkSelectionEntityDataSource} from "../bulk-selection.service";
import {ActionItem} from "../../_models/actionables/action-item";

@Component({
  selector: 'app-chapter-card',
  imports: [
    FormsModule,
    EntityTitleComponent,
    EntityCardComponent
  ],
  templateUrl: './chapter-card.component.html',
  styleUrl: './chapter-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterCardComponent implements OnInit, OnChanges {
  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly configFactory = inject(CardConfigFactory);

  protected readonly LibraryType = LibraryType;
  protected readonly MangaFormat = MangaFormat;

  @Input({required: true}) libraryId: number = 0;
  @Input({required: true}) seriesId: number = 0;
  @Input({required: true}) chapter!: Chapter;
  @Input({required: true}) libraryType!: LibraryType;

  index = input<number>(0);
  maxIndex = input<number>(1);
  dataSource = input<BulkSelectionEntityDataSource>('chapter');

  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<Chapter>[] = [];
  /**
   * If the entity is selected or not.
   */
  @Input() selected: boolean = false;
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;
  /**
   * This will suppress the "cannot read archive warning" when total pages is 0
   */
  @Input() suppressArchiveWarning: boolean = false;
  /**
   * When the card is selected.
   */
  @Output() selection = new EventEmitter<boolean>();
  /**
   * Emitted when the entity is deleted. Emits the entity id
   */
  @Output() reload: EventEmitter<number> = new EventEmitter();
  /**
   * Underlying data has mutated, mutated data is returned
   */
  @Output() dataChanged: EventEmitter<Chapter> = new EventEmitter();

  protected titleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');


  private user: User | undefined;

  private chapterSignal = signal<Chapter | null>(null);

  cardEntity = computed<CardEntity>(() => {
    const chapter = this.chapterSignal();
    if (!chapter) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.chapter({} as Chapter, 0, 0);
    }
    return CardEntityFactory.chapter(chapter, this.seriesId, this.libraryId);
  });

  config = computed<BaseCardConfiguration<Chapter>>(() => {
    return this.configFactory.forChapter({
      seriesId: this.seriesId,
      libraryId: this.libraryId,
      libraryType: this.libraryType,
      overrides: {
        allowSelection: this.allowSelection,
        actionableFunc: () => this.actions,
        selectionType: this.dataSource(),
        titleTemplate: this.titleTemplateRef()
      }
    });
  });

  ngOnInit() {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['chapter']) {
      this.chapterSignal.set(this.chapter);
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
