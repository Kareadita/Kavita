import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { Download } from 'src/app/shared/_models/download';

@Component({
  selector: 'app-download-indicator',
  templateUrl: './download-indicator.component.html',
  styleUrls: ['./download-indicator.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DownloadIndicatorComponent implements OnInit {

  /**
   * Observable that represents when the download completes
   */
  @Input() download$!: Observable<Download> | null;

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
  }

}
