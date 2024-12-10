import {
  ChangeDetectionStrategy, ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit,
} from '@angular/core';
import {AsyncPipe} from "@angular/common";
import {Observable, shareReplay, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {AccountService} from "../../../_services/account.service";
import {DownloadEvent, DownloadService} from "../../../shared/_services/download.service";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {Chapter} from "../../../_models/chapter";
import {Volume} from "../../../_models/volume";
import {Series} from "../../../_models/series";

@Component({
  selector: 'app-download-button',
  standalone: true,
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
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly downloadService = inject(DownloadService);

  @Input({required: true}) download$: Observable<DownloadEvent | null> | null = null;
  @Input({required: true}) entity!: Series | Volume | Chapter;
  @Input({required: true}) entityType: 'series' | 'volume' | 'chapter' = 'series';

  isDownloading = false;
  canDownload$: Observable<boolean> = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(u => !!u && (this.accountService.hasAdminRole(u) || this.accountService.hasDownloadRole(u)),
    shareReplay({bufferSize: 1, refCount: true})
  ));

  ngOnInit() {
    if (this.download$ != null) {
      this.download$.pipe(takeUntilDestroyed(this.destroyRef), tap(d => {
        if (d && d.progress >= 100) {
          this.isDownloading = false;
          this.cdRef.markForCheck();
        }
      })).subscribe();
    }
  }

  downloadClicked() {
    if (this.isDownloading) return;

    this.downloadService.download(this.entityType, this.entity, d => {
      this.isDownloading = !!d;
      this.cdRef.markForCheck();
    });
  }

}
