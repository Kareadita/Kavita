import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgxDatatableModule} from '@siemens/ngx-datatable';
import {TranslocoDirective, translate} from '@jsverse/transloco';
import {ToastrService} from '@openng/ngx-toastr';
import {finalize} from 'rxjs';
import {AccountService} from '../../../../_services/account.service';
import {ConfirmService} from '../../../../shared/confirm.service';
import {KoboService} from '../../../../_services/kobo.service';
import {ModalService} from '../../../../_services/modal.service';
import {KoboSyncPoint} from '../../../../_models/preferences/kobo-sync-point';
import {ModalResult} from '../../../../_models/modal/modal-result';
import {TimeDifferencePipe} from '../../../../_pipes/time-difference.pipe';
import {UtcToLocalDatePipe} from '../../../../_pipes/utc-to-locale-date.pipe';
import {UtcToLocalTimePipe} from '../../../../_pipes/utc-to-local-time.pipe';
import {ResponsiveTableComponent} from '../../../../shared/_components/responsive-table/responsive-table.component';
import {LoadingComponent} from '../../../../shared/loading/loading.component';
import {EmptyStateComponent} from '../../../../shared/_components/empty-state/empty-state.component';
import {patchEntitySignal} from '../../../../../libs/patch';
import {RestoreChaptersModalComponent} from '../restore-chapters-modal/restore-chapters-modal.component';
import {BiTruncatePipe} from "../../../../_pipes/bi-truncate.pipe";
import {Clipboard} from "@angular/cdk/clipboard";

@Component({
  selector: 'app-manage-kobo-sync-points',
  imports: [
    TranslocoDirective,
    NgbTooltip,
    NgxDatatableModule,
    ResponsiveTableComponent,
    LoadingComponent,
    EmptyStateComponent,
    TimeDifferencePipe,
    UtcToLocalDatePipe,
    UtcToLocalTimePipe,
    BiTruncatePipe
  ],
  templateUrl: './manage-kobo-sync-points.component.html',
  styleUrl: './manage-kobo-sync-points.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageKoboSyncPointsComponent implements OnInit {

  private readonly koboService = inject(KoboService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(ModalService);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  protected readonly clipboard = inject(Clipboard);

  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;
  protected readonly syncPoints = signal<KoboSyncPoint[]>([]);
  protected readonly syncPointsLoading = signal(true);
  protected readonly syncPointsError = signal(false);

  protected readonly trackBySyncPoint = (_: number, point: KoboSyncPoint) => point.id;

  ngOnInit(): void {
    this.loadSyncPoints();
  }

  protected openRemovedChapters(point: KoboSyncPoint) {
    const ref = this.modalService.open(RestoreChaptersModalComponent);
    ref.setInput('syncPoint', point);

    ref.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result: ModalResult<number>) => {
      if (!result.success || !result.data) return;

      patchEntitySignal(this.syncPoints, {
        ...point,
        removedChapterCount: result.data ?? 0
      });
    });
  }

  protected async deleteSyncPoint(point: KoboSyncPoint) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-kobo-sync-point'))) return;

    this.koboService.deleteSyncPoint(point.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.syncPoints.update(points => points.filter(p => p.id !== point.id));
        this.toastr.success(translate('toasts.kobo-sync-point-deleted'));
      });
  }

  private loadSyncPoints() {
    this.syncPointsLoading.set(true);
    this.syncPointsError.set(false);

    this.koboService.getSyncPoints().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.syncPointsLoading.set(false))
    ).subscribe({
      next: points => this.syncPoints.set([...points].sort((a, b) =>
        new Date(b.lastSync).getTime() - new Date(a.lastSync).getTime())),
      error: () => this.syncPointsError.set(true)
    });
  }
}
