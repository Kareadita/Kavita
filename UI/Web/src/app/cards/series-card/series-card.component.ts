import {
  ChangeDetectionStrategy,
  Component,
  computed,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  signal,
  SimpleChanges
} from '@angular/core';
import {Router} from '@angular/router';
import {NgbOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {FormsModule} from "@angular/forms";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {Series} from "../../_models/series";
import {RelationKind} from "../../_models/series-detail/relation-kind";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {ProgressUpdateResult} from "../../_models/card/card-configuration";

@Component({
  selector: 'app-series-card',
  imports: [FormsModule, EntityCardComponent],
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent implements OnChanges {

  private readonly router = inject(Router);
  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly configFactory = inject(CardConfigFactory);

  // ============================================================
  // EXISTING PUBLIC API (maintained for backwards compatibility)
  // ============================================================

  @Input({ required: true }) series!: Series;
  @Input() libraryId = 0;
  @Input() suppressLibraryLink = false;
  @Input() selected = false;
  @Input() allowSelection = false;
  @Input() relation: RelationKind | undefined = undefined;
  @Input() isOnDeck = false;
  @Input() previewOnClick = false;
  @Input() index = 0;
  @Input() maxIndex = 1;

  @Output() reload = new EventEmitter<number>();
  @Output() dataChanged = new EventEmitter<Series>();
  /** Emitted when a progress update is processed. */
  @Output() progressUpdated = new EventEmitter<ProgressUpdateResult<Series>>();

  private seriesSignal = signal<Series | null>(null);
  private relationSignal = signal<RelationKind | undefined>(undefined);
  private isOnDeckSignal = signal(false);

  cardEntity = computed<CardEntity>(() => {
    const series = this.seriesSignal();
    if (!series) {
      return CardEntityFactory.series({} as Series);
    }
    return CardEntityFactory.series(series, {
      relation: this.relationSignal(),
      isOnDeck: this.isOnDeckSignal()
    });
  });

  config = computed(() => {
    return this.configFactory.forSeries({
      overrides: {
        allowSelection: this.allowSelection,
        clickFunc: this.handleClick.bind(this)
      }
    });
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['series']) {
      this.seriesSignal.set(this.series);
    }
    if (changes['relation']) {
      this.relationSignal.set(this.relation);
    }
    if (changes['isOnDeck']) {
      this.isOnDeckSignal.set(this.isOnDeck);
    }
  }

  onDataChanged(entity: Series) {
    this.seriesSignal.set({...entity});
    this.dataChanged.emit(entity);
  }

  onProgressUpdated(result: ProgressUpdateResult<Series>) {
    if (result.requiresRefetch) {
      this.reload.emit(result.entity!.id);
      return;
    }
    
    this.onDataChanged(result.entity!);
  }

  private async handleClick(series: Series) {
    if (this.previewOnClick) {
      const ref = this.offcanvasService.open(SeriesPreviewDrawerComponent, {
        position: 'end',
        panelClass: ''
      });
      ref.componentInstance.isExternalSeries = false;
      ref.componentInstance.seriesId = series.id;
      ref.componentInstance.libraryId = series.libraryId;
      ref.componentInstance.name = series.name;
      return;
    }

    await this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }
}
