import {ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, model} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {ReaderSettingsComponent} from "../../reader-settings/reader-settings.component";
import {ReadingProfile} from "../../../../_models/preferences/reading-profiles";
import {TranslocoDirective} from "@jsverse/transloco";

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

//  updated = new EventEmitter<ReaderSettingUpdate>();


  constructor() {

    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }
    });
  }

  //
  // updateColorTheme(theme: BookTheme) {
  //   const evt = {setting: 'theme', object: theme} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // updateReaderStyles(pageStyles: PageStyle) {
  //   const evt = {setting: 'pageStyle', object: pageStyles} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // showPaginationOverlay(clickToPaginate: boolean) {
  //   const evt = {setting: 'clickToPaginate', object: clickToPaginate} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // toggleFullscreen() {
  //   const evt = {setting: 'fullscreen', object: null} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // updateWritingStyle(writingStyle: WritingStyle) {
  //   const evt = {setting: 'writingStyle', object: writingStyle} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // updateLayoutMode(mode: BookPageLayoutMode) {
  //   const evt = {setting: 'layoutMode', object: mode} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // updateReadingDirection(readingDirection: ReadingDirection) {
  //   const evt = {setting: 'readingDirection', object: readingDirection} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }
  //
  // updateImmersiveMode(immersiveMode: boolean) {
  //   const evt = {setting: 'immersiveMode', object: immersiveMode} as ReaderSettingUpdate;
  //   this.updated.emit(evt);
  // }

  close() {
    this.activeOffcanvas.close();
  }
}
