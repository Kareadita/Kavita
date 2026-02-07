import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
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
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {Chapter} from "../../_models/chapter";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FormsModule} from "@angular/forms";
import {EntityTitleComponent} from "../entity-title/entity-title.component";
import {filter, map} from "rxjs/operators";
import {UserProgressUpdateEvent} from "../../_models/events/user-progress-update-event";
import {LibraryType} from "../../_models/library/library";
import {MangaFormat} from "../../_models/manga-format";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {BaseCardConfiguration} from "../../_models/card/card-configuration";
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
  private readonly cdRef = inject(ChangeDetectorRef);
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
    return this.configFactory.forChapter(
      this.seriesId,
      this.libraryId,
      this.libraryType,
      {
        allowSelection: this.allowSelection,
        actionableFunc: () => this.actions,
        selectionType: this.dataSource(),
        titleTemplate: this.titleTemplateRef()
      }
    );
  });

  ngOnInit() {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });

    // TODO: I don't think we can port this easily. It might be worth just removing this functionality from the app
    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
      map(evt => evt.payload as UserProgressUpdateEvent), takeUntilDestroyed(this.destroyRef)).subscribe( updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (updateEvent.chapterId !== this.chapter.id) return;

      this.chapter.pagesRead = updateEvent.pagesRead;
      this.onDataChanged(this.chapter);
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
}
