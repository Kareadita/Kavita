import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input} from '@angular/core';
import {FilterService} from "../../../_services/filter.service";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {ActionService} from "../../../_services/action.service";
import {NgbModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {RouterLink} from "@angular/router";
import {APP_BASE_HREF} from "@angular/common";
import {EditSmartFilterModalComponent} from "../edit-smart-filter-modal/edit-smart-filter-modal.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
    selector: 'app-manage-smart-filters',
    imports: [ReactiveFormsModule, TranslocoDirective, FilterPipe, NgbTooltip],
    templateUrl: './manage-smart-filters.component.html',
    styleUrls: ['./manage-smart-filters.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent {

  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionService = inject(ActionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modelService = inject(NgbModal);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  @Input() target: '_self' | '_blank' = '_blank';

  filters: Array<SmartFilter> = [];
  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });

  filterList = (listItem: SmartFilter) => {
    const filterVal = (this.listForm.value.filterQuery || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }

  constructor() {
    this.loadData();
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters = filters;
      this.cdRef.markForCheck();
    });
  }

  resetFilter() {
    this.listForm.get('filterQuery')?.setValue('');
    this.cdRef.markForCheck();
  }

  isErrored(filter: SmartFilter) {
    return !decodeURIComponent(filter.filter).includes('¦');
  }

  async deleteFilter(f: SmartFilter) {
    await this.actionService.deleteFilter(f.id, success => {
      if (!success) return;
      this.resetFilter();
      this.loadData();
    });
  }

  editFilter(f: SmartFilter) {
    const modalRef = this.modelService.open(EditSmartFilterModalComponent, {  size: 'xl', fullscreen: 'md' });
    modalRef.componentInstance.smartFilter = f;
    modalRef.componentInstance.allFilters = this.filters;
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result) => {
      if (result) {
        this.resetFilter();
        this.loadData();
      }
    });
  }

}
