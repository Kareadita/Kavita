import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {APP_BASE_HREF, NgClass} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {StreamNamePipe} from "../../../_pipes/stream-name.pipe";
import {StreamType} from "../../../_models/dashboard/stream-type.enum";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";

@Component({
  selector: 'app-dashboard-stream-list-item',
  imports: [TranslocoDirective, StreamNamePipe, NgClass],
  templateUrl: './dashboard-stream-list-item.component.html',
  styleUrls: ['./dashboard-stream-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStreamListItemComponent {
  private readonly safeUrlPipe = new SafeUrlPipe();

  // TODO: Investigate one component for DashboardStream and SideNavStreamListItem
  item = input.required<DashboardStream>();
  position = input.required<number>();
  loadFilterUrl = computed(() => {
    return this.baseUrl + FilterUtilitiesService.getFilterLink(this.item().entityType, this.item().smartFilterEncoded ?? '');
  });

  hide = output<DashboardStream>();
  delete = output<DashboardStream>();

  protected readonly baseUrl = inject(APP_BASE_HREF);
  protected readonly StreamType = StreamType;
}
