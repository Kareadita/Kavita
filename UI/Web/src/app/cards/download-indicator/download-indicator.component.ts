import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Observable } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';
import { DownloadEvent } from 'src/app/shared/_services/download.service';
import {CommonModule} from "@angular/common";
import {CircularLoaderComponent} from "../../shared/circular-loader/circular-loader.component";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-download-indicator',
  standalone: true,
  imports: [CommonModule, CircularLoaderComponent, TranslocoDirective],
  templateUrl: './download-indicator.component.html',
  styleUrls: ['./download-indicator.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadIndicatorComponent {

  /**
   * Observable that represents when the download completes
   */
  @Input({required: true}) download$!: Observable<Download | DownloadEvent | null> | null;
}
