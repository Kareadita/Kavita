import {ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, model} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {ReaderSettingsComponent} from "../../reader-settings/reader-settings.component";
import {ReadingProfile} from "../../../../_models/preferences/reading-profiles";
import {TranslocoDirective} from "@jsverse/transloco";
import {EpubReaderSettingsService} from "../../../../_services/epub-reader-settings.service";

@Component({
  selector: 'app-epub-setting-drawer',
  imports: [
    ReaderSettingsComponent,
    TranslocoDirective
  ],
  templateUrl: './epub-setting-drawer.component.html',
  styleUrl: './epub-setting-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EpubSettingDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);

  chapterId = model<number>();
  seriesId = model<number>();
  readingProfile = model<ReadingProfile>();
  readerSettingsService = model.required<EpubReaderSettingsService>();

  constructor() {

    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }
    });
  }

  close() {
    this.activeOffcanvas.close();
  }
}
