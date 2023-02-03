import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Observable } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';
import { DownloadEvent } from 'src/app/shared/_services/download.service';

@Component({
  selector: 'app-download-indicator',
  templateUrl: './download-indicator.component.html',
  styleUrls: ['./download-indicator.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadIndicatorComponent {

  /**
   * Observable that represents when the download completes
   */
  @Input() download$!: Observable<Download | DownloadEvent | null> | null;

  constructor() { }
}
