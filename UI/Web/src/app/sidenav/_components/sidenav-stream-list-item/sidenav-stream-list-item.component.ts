import {ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output} from '@angular/core';
import {APP_BASE_HREF, CommonModule} from '@angular/common';
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";
import {RouterLink} from "@angular/router";

@Component({
  selector: 'app-sidenav-stream-list-item',
  standalone: true,
  imports: [CommonModule, StreamNamePipe, TranslocoDirective, RouterLink],
  templateUrl: './sidenav-stream-list-item.component.html',
  styleUrls: ['./sidenav-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SidenavStreamListItemComponent {
  @Input({required: true}) item!: SideNavStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<SideNavStream> = new EventEmitter<SideNavStream>();
  protected readonly SideNavStreamType = SideNavStreamType;
  protected readonly baseUrl = inject(APP_BASE_HREF);
}
