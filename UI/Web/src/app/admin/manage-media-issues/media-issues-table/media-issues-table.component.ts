import {ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {RouterLink} from "@angular/router";
import {NgTemplateOutlet} from "@angular/common";
import {KavitaMediaError} from "../../_models/media-error";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {Tracker} from "../../../shared/utils/Tracker";
import {MediaErrorReasonPipe} from "../../../_pipes/media-error-reason.pipe";
import {MediaErrorProducerPipe} from "../../../_pipes/media-error-producer.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";

@Component({
  selector: 'app-media-issues-table',
  imports: [TranslocoDirective, NgxDatatableModule, RouterLink, NgTemplateOutlet, ResponsiveTableComponent, MediaErrorReasonPipe,
    MediaErrorProducerPipe, UtcToLocalTimePipe, DefaultDatePipe],
  templateUrl: './media-issues-table.component.html',
  styleUrl: './media-issues-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaIssuesTableComponent {

  readonly rows = input.required<KavitaMediaError[]>();
  readonly dismissed = input<boolean>(false);
  readonly idPrefix = input.required<string>();

  readonly dismiss = output<number[]>();
  readonly restore = output<number[]>();

  protected readonly tracker = Tracker.IdTracker<KavitaMediaError>();
  protected readonly selectedIds = computed(() => this.tracker.selected().map(r => r.id));
  protected expandedIds = signal<ReadonlySet<number>>(new Set());

  protected readonly trackBy = (_: number, item: KavitaMediaError) => item.id;

  constructor() {
    effect(() => {
      const rows = this.rows();
      const selectedIds = untracked(() => new Set(this.selectedIds()));
      this.tracker.setData(rows, false);
      rows.filter(r => selectedIds.has(r.id)).forEach(r => this.tracker.toggle(r, true));
    });
  }

  protected toggleAll() {
    this.tracker.setAll(!this.tracker.allSelected());
  }

  protected toggleDetails(id: number) {
    this.expandedIds.update(ids => {
      const next = new Set(ids);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected apply(ids: number[]) {
    if (ids.length === 0) return;

    if (this.dismissed()) {
      this.restore.emit(ids);
    } else {
      this.dismiss.emit(ids);
    }
  }

  protected fileName(path: string) {
    return path.split('/').pop() ?? path;
  }

  protected folder(path: string) {
    const index = path.lastIndexOf('/');
    return index < 0 ? '' : path.substring(0, index);
  }
}
