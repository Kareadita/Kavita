import {ChangeDetectionStrategy, Component, input, model, output} from '@angular/core';
import {NgStyle} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

export class DrawerOptions {
  /**
   * Pixels to offset from the top of the screen. Only applies to position left/right
   */
  topOffset: number = 0;
}

@Component({
  selector: 'app-drawer',
  imports: [TranslocoDirective, NgStyle],
  templateUrl: './drawer.component.html',
  styleUrls: ['./drawer.component.scss'],
  exportAs: "drawer",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DrawerComponent {

  isOpen = model<boolean>(false);
  width = input<number>(400);
  /**
   * Side of the screen the drawer should animate from
   */
  position = input<'start' | 'end' | 'bottom' | 'top'>('start');
  options = input<Partial<DrawerOptions>>(new DrawerOptions());
  drawerClosed = output<boolean>();
  isOpenChange = output<boolean>();

  close() {
    this.isOpen.set(false);
    this.isOpenChange.emit(false);
    this.drawerClosed.emit(false);
  }
}
