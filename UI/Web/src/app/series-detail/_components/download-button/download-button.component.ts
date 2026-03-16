import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {AccountService} from "../../../_services/account.service";
import {DownloadService} from '../../../shared/_services/download.service';
import {DownloadEntityType} from '../../../shared/_models/download-queue-item';
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {Chapter} from "../../../_models/chapter";
import {Volume} from "../../../_models/volume";
import {Series} from "../../../_models/series";

@Component({
    selector: 'app-download-button',
    imports: [
        NgbTooltip,
        TranslocoDirective
    ],
    templateUrl: './download-button.component.html',
    styleUrl: './download-button.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadButtonComponent {

  private readonly accountService = inject(AccountService);
  private readonly downloadService = inject(DownloadService);

  entity = input.required<Series | Volume | Chapter>();
  seriesId = input.required<number>();
  libraryId = input.required<number>();
  entityType = input<DownloadEntityType>(DownloadEntityType.Series);

  isDownloading = computed(() => {
    const item = this.downloadService.getItemForEntity(this.entity());
    return item !== null && (item.status === 'queued' || item.status === 'preparing' || item.status === 'downloading');
  });
  canDownload = computed(() => this.accountService.hasAdminRole() || this.accountService.hasDownloadRole());

  downloadClicked() {
    if (this.isDownloading()) return;

    this.downloadService.download(this.entityType(), this.entity(), this.libraryId(), this.seriesId());
  }

}
