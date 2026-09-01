import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {EditExternalSourceItemComponent} from "../edit-external-source-item/edit-external-source-item.component";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {WikiLink} from "../../../_models/wiki";
import {EmptyStateComponent} from "../../../shared/_components/empty-state/empty-state.component";
import {toSignal} from "@angular/core/rxjs-interop";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

@Component({
    selector: 'app-manage-external-sources',
  imports: [FormsModule, ReactiveFormsModule, TranslocoDirective, EditExternalSourceItemComponent, EmptyStateComponent, FormFieldDirective],
    templateUrl: './manage-external-sources.component.html',
    styleUrls: ['./manage-external-sources.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageExternalSourcesComponent {
  protected readonly accountService = inject(AccountService);
  private readonly externalSourceService = inject(ExternalSourceService);

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });
  externalSources = signal<ExternalSource[]>([]);
  private readonly filterQuery = toSignal(this.listForm.controls.filterQuery.valueChanges, {initialValue: ''});
  filteredExternalSources = computed(() => {
    const data = this.externalSources();
    const query = (this.filterQuery() || '').toLowerCase();
    if (query === '') return data;

    return data.filter(listItem => listItem.name.toLowerCase().indexOf(query) >= 0 || listItem.host.toLowerCase().indexOf(query) >= 0);
  });



  filterList = (listItem: ExternalSource) => {
    const filterVal = (this.listForm.value.filterQuery || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0 || listItem.host.toLowerCase().indexOf(filterVal) >= 0;
  }

  constructor() {
    this.externalSourceService.getExternalSources().subscribe(data => {
      this.externalSources.set(data);
    });
  }

  resetFilter() {
    this.listForm.get('filterQuery')?.setValue('');
  }

  addNewExternalSource() {
    this.externalSources.update(sources => [
      {id: 0, name: '', host: '', apiKey: ''},
      ...sources
    ]);
  }

  updateSource(index: number, updatedSource: ExternalSource) {
    this.externalSources.update(sources =>
      sources.map((source, i) => i === index ? updatedSource : source)
    );
  }

  deleteSource(index: number, updatedSource: ExternalSource) {
    this.externalSources.update(sources =>
      sources.filter((_, i) => i !== index)
    );
    this.resetFilter();
  }

  protected readonly WikiLink = WikiLink;
}
