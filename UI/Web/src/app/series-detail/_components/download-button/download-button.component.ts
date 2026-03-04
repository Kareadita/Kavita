import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal,} from '@angular/core';
import {AsyncPipe} from "@angular/common";
import {Observable, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AccountService} from "../../../_services/account.service";
import {DownloadEvent, DownloadService} from "../../../shared/_services/download.service";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {Chapter} from "../../../_models/chapter";
import {Volume} from "../../../_models/volume";
import {Series} from "../../../_models/series";

@Component({
    selector: 'app-download-button',
    imports: [
        AsyncPipe,
        NgbTooltip,
        TranslocoDirective
    ],
    templateUrl: './download-button.component.html',
    styleUrl: './download-button.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadButtonComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly downloadService = inject(DownloadService);

  download$ = input.required<Observable<DownloadEvent | null> | null>();
  entity = input.required<Series | Volume | Chapter>();
  entityType = input<'series' | 'volume' | 'chapter'>('series');
  readonly libraryId = input<number>(0);

  isDownloading = signal<boolean>(false);
  canDownload = computed(() => this.accountService.hasAdminRole() || this.accountService.hasDownloadRole());

  ngOnInit() {
    const downloadObservable = this.download$();
    if (downloadObservable != null) {
      downloadObservable.pipe(takeUntilDestroyed(this.destroyRef), tap(d => {
        if (d && d.progress >= 100) {
          this.isDownloading.set(false);
        }
      })).subscribe();
    }
  }

  downloadClicked() {
    if (this.isDownloading()) return;

    this.downloadService.download(this.entityType(), this.entity(), d => {
      this.isDownloading.set(!!d);
    }, this.libraryId());
  }

}
