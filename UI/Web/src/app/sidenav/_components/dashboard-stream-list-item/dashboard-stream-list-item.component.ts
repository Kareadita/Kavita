import {ChangeDetectionStrategy, Component, inject, input, output} from '@angular/core';
import {APP_BASE_HREF, NgClass} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {StreamType} from "../../../_models/dashboard/stream-type.enum";

@Component({
  selector: 'app-dashboard-stream-list-item',
  imports: [TranslocoDirective, StreamNamePipe, NgClass],
  templateUrl: './dashboard-stream-list-item.component.html',
  styleUrls: ['./dashboard-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStreamListItemComponent {
  // TODO: Investigate one component for DashboardStream and SideNavStreamListItem
  item = input.required<DashboardStream>();
  position = input.required<number>();

  hide = output<DashboardStream>();
  delete = output<DashboardStream>();

  protected readonly baseUrl = inject(APP_BASE_HREF);
  protected readonly StreamType = StreamType;
}
