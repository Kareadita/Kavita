import {
  ChangeDetectionStrategy,
  Component,
  computed,
  EventEmitter,
  inject,
  input,
  Input,
  OnChanges,
  Output,
  signal,
  SimpleChanges
} from '@angular/core';
import {Router} from "@angular/router";
import {ImageService} from "../../_services/image.service";
import {Volume} from "../../_models/volume";
import {UtilityService} from "../../shared/_services/utility.service";
import {LibraryType} from "../../_models/library/library";
import {FormsModule} from "@angular/forms";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {BaseCardConfiguration, ProgressUpdateResult} from "../../_models/card/card-configuration";
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
export class VolumeCardComponent implements OnChanges {

  public readonly imageService = inject(ImageService);
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

  private readonly volumeSignal = signal<Volume | null>(null);

  readonly cardEntity = computed<CardEntity>(() => {
    const volume = this.volumeSignal();
    if (!volume) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.volume({} as Volume, 0, 0);
    }
    return CardEntityFactory.volume(volume, this.seriesId, this.libraryId);
  });

  readonly config = computed<BaseCardConfiguration<Volume>>(() => {
    const baseConfig = this.configFactory.forVolume({
        seriesId: this.seriesId,
        libraryId: this.libraryId,
        libraryType: this.libraryType,
        overrides: {
          allowSelection: this.allowSelection,
          clickFunc: this.handleClick.bind(this),
          actionableFunc: (_) => this.actions,
        }
      });

    return baseConfig;
  });


  ngOnChanges(changes: SimpleChanges): void {
    if (changes['volume']) {
      this.volumeSignal.set(this.volume);
    }
  }

  onProgressUpdated(result: ProgressUpdateResult<Volume>) {
    if (result.requiresRefetch) {
      this.reload.emit(result.entity!.id);
      return;
    }

    this.onDataChanged(result.entity!);
  }


  handleClick(event: any) {
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'volume', this.volume.id]);
  }

  onDataChanged(entity: Volume) {
    this.volumeSignal.set({...entity});
    this.dataChanged.emit(entity);
  }
}
