import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {NgbActiveOffcanvas} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaplusTooltipComponent} from '../kavitaplus-tooltip/kavitaplus-tooltip.component';
import {OffCanvasResizeComponent, ResizeMode} from '../../../shared/_components/off-canvas-resize/off-canvas-resize.component';
import {BreakpointService} from '../../../_services/breakpoint.service';

@Component({
  selector: 'app-kavitaplus-drawer',
  templateUrl: './kavitaplus-drawer.component.html',
  styleUrls: ['./kavitaplus-drawer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, KavitaplusTooltipComponent, OffCanvasResizeComponent],
})
export class KavitaplusDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  readonly breakpointService = inject(BreakpointService);

  seriesId = input.required<number>();

  protected readonly ResizeMode = ResizeMode;
  protected readonly window = window;

  close() {
    this.activeOffcanvas.close();
  }
}
