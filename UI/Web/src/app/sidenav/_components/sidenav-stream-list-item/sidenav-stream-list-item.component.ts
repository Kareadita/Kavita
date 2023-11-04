import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";

@Component({
  selector: 'app-sidenav-stream-list-item',
  standalone: true,
  imports: [CommonModule, StreamNamePipe, TranslocoDirective],
  templateUrl: './sidenav-stream-list-item.component.html',
  styleUrls: ['./sidenav-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SidenavStreamListItemComponent {
  @Input({required: true}) item!: SideNavStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<SideNavStream> = new EventEmitter<SideNavStream>();
  protected readonly SideNavStreamType = SideNavStreamType;
}
