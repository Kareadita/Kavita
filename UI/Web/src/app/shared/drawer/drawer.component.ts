import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output } from '@angular/core';
import {CommonModule} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

export class DrawerOptions {
  /**
   * Pixels to offset from the top of the screen. Only applies to postion left/right
   */
  topOffset: number = 0;
}

@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [CommonModule, TranslocoDirective],
  templateUrl: './drawer.component.html',
  styleUrls: ['./drawer.component.scss'],
  exportAs: "drawer",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DrawerComponent {
  @Input() isOpen = false;
  @Input() width: number = 400;
  /**
   * Side of the screen the drawer should animate from
   */
  @Input() position: 'start' | 'end' | 'bottom' | 'top' = 'start';
  @Input() options: Partial<DrawerOptions> = new DrawerOptions();
  @Output() drawerClosed = new EventEmitter();
  @Output() isOpenChange: EventEmitter<boolean> = new EventEmitter();

  constructor(private readonly cdRef: ChangeDetectorRef) {}

  close() {
    this.isOpen = false;
    this.isOpenChange.emit(false);
    this.drawerClosed.emit(false);
    this.cdRef.markForCheck();
  }
}
