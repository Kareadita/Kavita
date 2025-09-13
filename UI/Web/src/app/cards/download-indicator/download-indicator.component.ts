import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {Observable} from 'rxjs';
import {Download} from 'src/app/shared/_models/download';
import {DownloadEvent} from 'src/app/shared/_services/download.service';
import {CircularLoaderComponent} from "../../shared/circular-loader/circular-loader.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {AsyncPipe} from "@angular/common";

@Component({
  selector: 'app-download-indicator',
  imports: [CircularLoaderComponent, TranslocoDirective, AsyncPipe],
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
