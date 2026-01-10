import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  inject,
  input,
  output,
  TemplateRef,
  TrackByFunction
} from '@angular/core';
import {Breakpoint, UtilityService} from "../../_services/utility.service";
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

export interface PageEvent {
  pageIndex: number;
  pageSize: number;
}


/**
 * A one-stop shop to provide a responsive experience
 */
@Component({
  selector: 'app-responsive-table',
  imports: [
    NgTemplateOutlet,
    TranslocoDirective
  ],
  templateUrl: './responsive-table.component.html',
  styleUrl: './responsive-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResponsiveTableComponent<T> {
  private readonly utilityService = inject(UtilityService);

  rows = input.required<T[]>();
  breakpoint = input<Breakpoint>(Breakpoint.Mobile);
  trackByFn = input<TrackByFunction<T>>((index, item: any) => item?.id ?? index);

  totalItems = input<number>(0);
  pageSize = input<number>(10);
  pageIndex = input<number>(0);

  pageChange = output<PageEvent>();

  private cardTemplateSignal = contentChild<TemplateRef<{$implicit: T, index: number}>>('cardTemplate');
  protected readonly cardTemplateRef = computed(() => this.cardTemplateSignal());

  private tableTemplateSignal = contentChild<TemplateRef<void>>('tableTemplate');
  protected readonly tableTemplateRef = computed(() => this.tableTemplateSignal());

  protected readonly showCards = computed(() => {
    const activeBreakpoint = this.utilityService.activeBreakpointSignal();
    const setting = this.breakpoint();
    return activeBreakpoint && activeBreakpoint <= setting;
  });

  protected readonly isEmpty = computed(() => this.rows().length === 0);

  protected readonly totalPages = computed(() =>
    Math.ceil(this.totalItems() / this.pageSize())
  );

  protected onPageChange(newIndex: number) {
    this.pageChange.emit({ pageIndex: newIndex, pageSize: this.pageSize() });
  }
}
