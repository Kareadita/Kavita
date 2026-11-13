import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {NgbActiveModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {finalize} from 'rxjs';
import {TranslocoDirective, translate} from '@jsverse/transloco';
import {ToastrService} from '@openng/ngx-toastr';
import {KoboSyncPoint} from '../../../../_models/preferences/kobo-sync-point';
import {ChapterRestoreInfo} from '../../../../_models/chapter-restore-info';
import {KoboService} from '../../../../_services/kobo.service';
import {ImageService} from '../../../../_services/image.service';
import {ImageComponent} from '../../../../shared/image/image.component';
import {LoadingComponent} from '../../../../shared/loading/loading.component';
import {EmptyStateComponent} from '../../../../shared/_components/empty-state/empty-state.component';
import {modalSaved} from '../../../../_models/modal/modal-result';
import {Tracker} from "../../../../shared/utils/Tracker";
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent
} from "@siemens/ngx-datatable";
import {UtcToLocalDatePipe} from "../../../../_pipes/utc-to-locale-date.pipe";
import {TimeDifferencePipe} from "../../../../_pipes/time-difference.pipe";
import {UtcToLocalTimePipe} from "../../../../_pipes/utc-to-local-time.pipe";

@Component({
  selector: 'app-restore-chapters-modal',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    EmptyStateComponent,
    DatatableComponent,
    DataTableColumnDirective,
    DataTableColumnHeaderDirective,
    DataTableColumnCellDirective,
    UtcToLocalDatePipe,
    TimeDifferencePipe,
    NgbTooltip,
    UtcToLocalTimePipe
  ],
  templateUrl: './restore-chapters-modal.component.html',
  styleUrl: './restore-chapters-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RestoreChaptersModalComponent implements OnInit {

  private readonly modalRef = inject(NgbActiveModal);
  private readonly koboService = inject(KoboService);
  private readonly imageService = inject(ImageService);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);

  syncPoint = input.required<KoboSyncPoint>();

  protected readonly restoreInfo = signal<ChapterRestoreInfo[]>([]);
  protected readonly infoLoading = signal(true);
  protected readonly infoError = signal(false);
  protected readonly tracker = new Tracker<number>((a, b) => a === b);
  protected readonly restoring = signal(false);

  ngOnInit() {
    this.koboService.getChapterRestoreInfo(this.syncPoint().id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.infoLoading.set(false))
      )
      .subscribe({
        next: info => {
          this.restoreInfo.set(info);
          this.tracker.setData(info.map(i => i.chapterId), false);
        },
        error: () => this.infoError.set(true)
      });
  }

  restore() {
    const ids = [...this.tracker.selected()];
    if (ids.length === 0 || this.restoring()) return;

    this.restoring.set(true);
    this.koboService.undoRemoveBooks(this.syncPoint().id, ids)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.restoring.set(false))
      )
      .subscribe(() => {
        this.toastr.success(translate('toasts.kobo-chapters-restored'));
        this.modalRef.close(modalSaved(ids.length));
      });
  }

  close() {
    this.modalRef.dismiss();
  }
}
