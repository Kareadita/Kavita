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
  SimpleChanges
} from '@angular/core';
import {Router} from "@angular/router";
import {ImageService} from "../../_services/image.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {AccountService} from "../../_services/account.service";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, map} from "rxjs/operators";
import {UserProgressUpdateEvent} from "../../_models/events/user-progress-update-event";
import {Volume} from "../../_models/volume";
import {UtilityService} from "../../shared/_services/utility.service";
import {LibraryType} from "../../_models/library/library";
import {FormsModule} from "@angular/forms";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {BaseCardConfiguration} from "../../_models/card/card-configuration";
import {ActionItem} from "../../_models/actionables/action-item";

@Component({
  selector: 'app-volume-card',
  imports: [
    FormsModule,
    EntityCardComponent,
  ],
  templateUrl: './volume-card.component.html',
  styleUrl: './volume-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VolumeCardComponent implements OnInit, OnChanges {

  private readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  protected readonly utilityService = inject(UtilityService);
  private readonly configFactory = inject(CardConfigFactory);

  index = input.required<number>();
  maxIndex = input.required<number>();

  // ============================================================
  // EXISTING PUBLIC API (maintained for backwards compatibility)
  // ============================================================

  @Input({required: true}) libraryId: number = 0;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) seriesId: number = 0;
  @Input({required: true}) volume!: Volume;
  /**
   * Any actions to perform on the card
   */
  @Input() actions: ActionItem<Volume>[] = [];
  /**
   * If the entity should show selection code
   */
  @Input() allowSelection: boolean = false;

  /**
   * Emitted when the entity is deleted. Emits the entity id
   */
  @Output() reload: EventEmitter<number> = new EventEmitter();
  /**
   * Underlying data has mutated, mutated data is returned
   */
  @Output() dataChanged: EventEmitter<Volume> = new EventEmitter();

  private user: User | undefined;

  private volumeSignal = signal<Volume | null>(null);

  cardEntity = computed<CardEntity>(() => {
    const volume = this.volumeSignal();
    if (!volume) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.volume({} as Volume, 0, 0);
    }
    return CardEntityFactory.volume(volume, this.seriesId, this.libraryId);
  });

  config = computed<BaseCardConfiguration<Volume>>(() => {
    const baseConfig = this.configFactory.forVolume(
      this.seriesId,
      this.libraryId,
      this.libraryType,
      {
        allowSelection: this.allowSelection,
        clickFunc: this.handleClick.bind(this),
        actionableFunc: (_) => this.actions,
      }
    );

    return baseConfig;
  });


  ngOnInit() {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      this.user = user;
    });

    // TODO: Decide if I want to port this feature over or leave it off going forward
    this.messageHub.messages$.pipe(filter(event => event.event === EVENTS.UserProgressUpdate),
      map(evt => evt.payload as UserProgressUpdateEvent), takeUntilDestroyed(this.destroyRef))
      .subscribe(updateEvent => {
      if (this.user === undefined || this.user.username !== updateEvent.username) return;
      if (updateEvent.volumeId !== this.volume.id) return;

      let sum = 0;
      const chapters = this.volume.chapters.filter(c => c.volumeId === updateEvent.volumeId);
      chapters.forEach(chapter => {
        chapter.pagesRead = updateEvent.pagesRead;
        sum += chapter.pagesRead;
      });
      this.volume.pagesRead = sum;
      this.onDataChanged(this.volume);
    });

  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['volume']) {
      this.volumeSignal.set(this.volume);
    }
  }


  handleClick(event: any) {
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'volume', this.volume.id]);
  }

  onDataChanged(entity: Volume) {
    this.volumeSignal.set({...entity});
    this.dataChanged.emit(entity);
  }
}
