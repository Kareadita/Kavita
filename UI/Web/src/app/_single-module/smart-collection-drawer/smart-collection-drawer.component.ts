import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {UserCollection} from "../../_models/collection-tag";
import {DecimalPipe} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {Series} from "../../_models/series";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {RouterLink} from "@angular/router";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {
  OffCanvasResizeComponent,
  ResizeMode
} from "../../shared/_components/off-canvas-resize/off-canvas-resize.component";
import {BreakpointService} from "../../_services/breakpoint.service";

@Component({
  selector: 'app-smart-collection-drawer',
  imports: [
    TranslocoDirective,
    SafeHtmlPipe,
    RouterLink,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    SettingItemComponent,
    DecimalPipe,
    OffCanvasResizeComponent
  ],
  templateUrl: './smart-collection-drawer.component.html',
  styleUrl: './smart-collection-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SmartCollectionDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  protected readonly breakpointService = inject(BreakpointService);

  collection = input.required<UserCollection>();
  series = input.required<Series[]>();

  close() {
    this.activeOffcanvas.close();
  }

  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;
}
