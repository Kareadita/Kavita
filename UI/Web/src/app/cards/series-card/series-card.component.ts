import {ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output, signal} from '@angular/core';
import {Router} from '@angular/router';
import {FormsModule} from "@angular/forms";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {Series} from "../../_models/series";
import {RelationKind} from "../../_models/series-detail/relation-kind";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {ProgressUpdateResult} from "../../_models/card/card-configuration";
import {DrawerService} from "../../_services/drawer.service";

@Component({
  selector: 'app-series-card',
  imports: [FormsModule, EntityCardComponent],
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent {

  private readonly router = inject(Router);
  private readonly drawerService = inject(DrawerService);
  private readonly configFactory = inject(CardConfigFactory);

  series = input.required<Series>();
  suppressLibraryLink = input<boolean>(false);
  allowSelection = input<boolean>(false);
  relation = input<RelationKind | undefined>(undefined);
  isOnDeck = input<boolean>(false);
  previewOnClick = input<boolean>(false);
  index = input<number>(0);
  maxIndex = input<number>(1);

  reload = output<number>();
  dataChanged = output<Series>();
  /** Emitted when a progress update is processed. */
  progressUpdated = output<ProgressUpdateResult<Series>>();

  private seriesSignal = linkedSignal(() => this.series());
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
        allowSelection: this.allowSelection(),
        clickFunc: this.handleClick.bind(this)
      }
    });
  });

  onDataChanged(entity: Series) {
    console.log('series updated, ', entity);
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
    if (this.previewOnClick()) {
      const ref = this.drawerService.open(SeriesPreviewDrawerComponent, {
        position: 'end',
        panelClass: ''
      });
      ref.setInput('isExternalSeries', false);
      ref.setInput('seriesId', series.id);
      ref.setInput('libraryId', series.libraryId);
      ref.setInput('name', series.name);

      return;
    }

    await this.router.navigate(['library', series.libraryId, 'series', series.id]);
  }
}
