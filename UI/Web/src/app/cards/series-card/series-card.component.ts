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
import {NgbModal, NgbOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {Action, ActionItem} from 'src/app/_services/action-factory.service';
import {SeriesService} from 'src/app/_services/series.service';
import {ActionService} from 'src/app/_services/action.service';
import {translate, TranslocoService} from "@jsverse/transloco";
import {FormsModule} from "@angular/forms";
import {DownloadService} from "../../shared/_services/download.service";
import {ReadingProfileService} from "../../_services/reading-profile.service";
import {EntityCardComponent} from "../entity-card/entity-card.component";
import {CardConfigFactory} from "../../_services/card-config-factory.service";
import {CardConfiguration} from "../../_models/card/card-configuration";
import {Series} from "../../_models/series";
import {EditSeriesModalComponent} from "../_modals/edit-series-modal/edit-series-modal.component";
import {RelationKind} from "../../_models/series-detail/relation-kind";
import {CardEntity, CardEntityFactory} from "../../_models/card/card-entity";
import {Device} from "../../_models/device/device";
import {SeriesPreviewDrawerComponent} from "../../_single-module/series-preview-drawer/series-preview-drawer.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";

@Component({
  selector: 'app-series-card',
  imports: [FormsModule, EntityCardComponent],
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeriesCardComponent implements OnChanges {

  private readonly router = inject(Router);
  private readonly modalService = inject(NgbModal);
  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly toastr = inject(ToastrService);
  private readonly translocoService = inject(TranslocoService);
  private readonly seriesService = inject(SeriesService);
  private readonly actionService = inject(ActionService);
  private readonly configFactory = inject(CardConfigFactory);
  private readonly downloadService = inject(DownloadService);
  private readonly readingProfilesService = inject(ReadingProfileService);

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

  @Output() clicked = new EventEmitter<Series>();
  @Output() reload = new EventEmitter<number>();
  @Output() dataChanged = new EventEmitter<Series>();
  @Output() selection = new EventEmitter<boolean>();

  private seriesSignal = signal<Series | null>(null);
  private relationSignal = signal<RelationKind | undefined>(undefined);
  private isOnDeckSignal = signal(false);

  cardEntity = computed<CardEntity>(() => {
    const series = this.seriesSignal();
    if (!series) {
      // Return a placeholder - shouldn't render in practice
      return CardEntityFactory.series({} as Series);
    }
    return CardEntityFactory.series(series, {
      relation: this.relationSignal(),
      isOnDeck: this.isOnDeckSignal()
    });
  });

  config = computed<CardConfiguration<Series>>(() => {
    const baseConfig = this.configFactory.forSeries(
      this.handleSeriesActionCallback.bind(this),
      {
        allowSelection: this.allowSelection,
        clickFunc: this.handleClick.bind(this)
      }
    );

    // Add On Deck action if needed
    if (this.isOnDeckSignal()) {
      return this.addOnDeckAction(baseConfig);
    }

    return baseConfig;
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

  // ============================================================
  // ACTION HANDLING (preserved from original implementation)
  // ============================================================

  // TODO: See if we can further streamline action handling without needing to implement handling in each component
  private async handleSeriesActionCallback(action: ActionItem<Series>, series: Series) {
    switch (action.action) {
      case Action.MarkAsRead:
        this.actionService.markSeriesAsRead(series, () => {
          series.pagesRead = series.pages;
          this.dataChanged.emit(series);
        });
        break;

      case Action.MarkAsUnread:
        this.actionService.markSeriesAsUnread(series, () => {
          series.pagesRead = 0;
          this.dataChanged.emit(series);
        });
        break;

      case Action.Scan:
        this.seriesService.scan(series.libraryId, series.id).subscribe(() => {
          this.toastr.success(translate('toasts.scan-queued', { name: series.name }));
        });
        break;

      case Action.RefreshMetadata:
        await this.actionService.refreshSeriesMetadata(series, undefined, true, true);
        break;

      case Action.GenerateColorScape:
        await this.actionService.refreshSeriesMetadata(series, undefined, false, false);
        break;

      case Action.Delete:
        await this.actionService.deleteSeries(series, (result) => {
          if (result) this.reload.emit(series.id);
        });
        break;

      case Action.Edit:
        this.openEditModal(series);
        break;

      case Action.Match:
        this.actionService.matchSeries(series, (refreshNeeded) => {
          if (refreshNeeded) this.reload.emit(series.id);
        });
        break;

      case Action.AddToReadingList:
        this.actionService.addSeriesToReadingList(series);
        break;

      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList([series.id]);
        break;

      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList([series.id]);
        if (this.router.url.startsWith('/want-to-read')) {
          this.reload.emit(series.id);
        }
        break;

      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag([series]);
        break;

      case Action.AnalyzeFiles:
        this.actionService.analyzeFilesForSeries(series);
        break;

      case Action.SendTo:
        const device = action._extra!.data as Device;
        this.actionService.sendSeriesToDevice(series.id, device);
        break;

      case Action.RemoveFromOnDeck:
        this.seriesService.removeFromOnDeck(series.id).subscribe(() => {
          this.reload.emit(series.id);
        });
        break;

      case Action.Download:
        this.downloadService.download('series', series);
        break;

      case Action.SetReadingProfile:
        this.actionService.setReadingProfileForMultiple([series]);
        break;

      case Action.ClearReadingProfile:
        this.readingProfilesService.clearSeriesProfiles(series.id).subscribe(() => {
          this.toastr.success(this.translocoService.translate('actionable.cleared-profile'));
        });
        break;
    }
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

    this.clicked.emit(series);
    await this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  private openEditModal(series: Series) {
    const modalRef = this.modalService.open(EditSeriesModalComponent, DefaultModalOptions);
    modalRef.componentInstance.series = series;
    modalRef.closed.subscribe((closeResult: { success: boolean; series: Series; coverImageUpdate: boolean }) => {
      if (closeResult.success) {
        this.seriesService.getSeries(series.id).subscribe(updated => {
          this.seriesSignal.set(updated);
          this.reload.emit(updated.id);
          this.dataChanged.emit(updated);
        });
      }
    });
  }

  private addOnDeckAction(config: CardConfiguration<Series>) {
    const actions = [...config.actionables];
    const othersIndex = actions.findIndex(a => a.title === 'others');

    if (othersIndex >= 0) {
      const othersAction = { ...actions[othersIndex] };
      const hasRemoveAction = othersAction.children?.some(c => c.action === Action.RemoveFromOnDeck);

      if (!hasRemoveAction && othersAction.children) {
        othersAction.children = [
          ...othersAction.children,
          {
            action: Action.RemoveFromOnDeck,
            title: 'remove-from-on-deck',
            description: '',
            callback: this.handleSeriesActionCallback.bind(this),
            class: 'danger',
            requiresAdmin: false,
            requiredRoles: [],
            shouldRender: () => true,
            children: []
          }
        ];
        actions[othersIndex] = othersAction;
      }
    }

    return { ...config, actionables: actions };
  }
}
