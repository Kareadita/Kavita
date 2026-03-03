import {ChangeDetectionStrategy, Component, inject, input, output} from '@angular/core';
import {APP_BASE_HREF, NgClass} from '@angular/common';
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";

@Component({
  selector: 'app-sidenav-stream-list-item',
  imports: [StreamNamePipe, TranslocoDirective, NgClass],
  templateUrl: './sidenav-stream-list-item.component.html',
  styleUrls: ['./sidenav-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SidenavStreamListItemComponent {
  item = input.required<SideNavStream>();
  position = input.required<number>();
  hide = output<SideNavStream>();
  delete = output<SideNavStream>();

  protected readonly SideNavStreamType = SideNavStreamType;
  protected readonly baseUrl = inject(APP_BASE_HREF);
}
