import {ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output} from '@angular/core';
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
  @Input({required: true}) item!: DashboardStream;
  @Input({required: true}) position: number = 0;
  @Output() hide: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();
  @Output() delete: EventEmitter<DashboardStream> = new EventEmitter<DashboardStream>();
  protected readonly baseUrl = inject(APP_BASE_HREF);
  protected readonly StreamType = StreamType;
}
