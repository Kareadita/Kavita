import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {EditExternalSourceItemComponent} from "../edit-external-source-item/edit-external-source-item.component";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {WikiLink} from "../../../_models/wiki";
import {EmptyStateComponent} from "../../../shared/_components/empty-state/empty-state.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {form, FormField} from "@angular/forms/signals";

@Component({
    selector: 'app-manage-external-sources',
  imports: [TranslocoDirective, EditExternalSourceItemComponent, EmptyStateComponent, FormFieldDirective, FormField],
    templateUrl: './manage-external-sources.component.html',
    styleUrls: ['./manage-external-sources.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageExternalSourcesComponent {
  protected readonly accountService = inject(AccountService);
  private readonly externalSourceService = inject(ExternalSourceService);

  formModel = signal({
    query: ''
  });
  formGroup = form(this.formModel);
  externalSources = signal<ExternalSource[]>([]);

  filteredExternalSources = computed(() => {
    const data = this.externalSources();
    const query = (this.formModel().query || '').toLowerCase();
    if (query === '') return data;

    return data.filter(listItem => listItem.name.toLowerCase().indexOf(query) >= 0 || listItem.host.toLowerCase().indexOf(query) >= 0);
  });

  constructor() {
    this.externalSourceService.getExternalSources().subscribe(data => {
      this.externalSources.set(data);
    });
  }

  resetFilter() {
    this.formGroup.query().value.set('');
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
