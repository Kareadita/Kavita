import {ChangeDetectionStrategy, Component, DestroyRef, inject, QueryList, signal, ViewChildren} from '@angular/core';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {SortableHeader} from 'src/app/_single-module/table/_directives/sortable-header.directive';
import {PieDataItem} from '../../_models/pie-data-item';
import {DecimalPipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent
} from "@siemens/ngx-datatable";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
    selector: 'app-publication-status-stats',
    templateUrl: './publication-status-stats.component.html',
    styleUrls: ['./publication-status-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, TranslocoDirective, ResponsiveTableComponent, DatatableComponent, DataTableColumnDirective, DataTableColumnHeaderDirective, DataTableColumnCellDirective, StatsNoDataComponent]
})
export class PublicationStatusStatsComponent {
  private readonly statService = inject(StatisticsService);


  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;

  publicationStatues = signal<Array<PieDataItem>>([]);
  view: [number, number] = [700, 400];

  private readonly destroyRef = inject(DestroyRef);

  formControl: FormControl = new FormControl(true, []);

  readonly trackByIdentity = (_: number, item: PieDataItem) => item.name + '_' + item.value;


  constructor() {
    this.statService.getPublicationStatus().subscribe(status => {
      this.publicationStatues.set(status);
    });
  }
}
