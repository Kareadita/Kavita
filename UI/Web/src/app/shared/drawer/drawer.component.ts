import { Component, EventEmitter, Input, Output } from '@angular/core';

export class DrawerOptions {
  /**
   * Pixels to offset from the top of the screen. Only applies to postion left/right
   */
  topOffset: number = 0;
}

@Component({
  selector: 'app-drawer',
  templateUrl: './drawer.component.html',
  styleUrls: ['./drawer.component.scss'],
  exportAs: "drawer"
})
export class DrawerComponent {

  @Input() isOpen = false;
  @Input() width: number = 400;
  /**
   * Side of the screen the drawer should animate from
   */
  @Input() position: 'left' | 'right' | 'bottom' = 'left';
  @Input() options: Partial<DrawerOptions> = new DrawerOptions();
  @Output() drawerClosed = new EventEmitter();


  close() {
    this.drawerClosed.emit();
  }
}
