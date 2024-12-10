import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgbCollapse, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {EditExternalSourceItemComponent} from "../edit-external-source-item/edit-external-source-item.component";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {WikiLink} from "../../../_models/wiki";

@Component({
  selector: 'app-manage-external-sources',
  standalone: true,
  imports: [FormsModule, NgOptimizedImage, NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgbCollapse, EditExternalSourceItemComponent, FilterPipe],
  templateUrl: './manage-external-sources.component.html',
  styleUrls: ['./manage-external-sources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageExternalSourcesComponent {

  externalSources: Array<ExternalSource> = [];
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly externalSourceService = inject(ExternalSourceService);
  protected readonly WikiLink = WikiLink;

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });

  filterList = (listItem: ExternalSource) => {
    const filterVal = (this.listForm.value.filterQuery || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0 || listItem.host.toLowerCase().indexOf(filterVal) >= 0;
  }

  constructor(public accountService: AccountService) {
    this.externalSourceService.getExternalSources().subscribe(data => {
      this.externalSources = data;
      this.cdRef.markForCheck();
    });
  }

  resetFilter() {
    this.listForm.get('filterQuery')?.setValue('');
    this.cdRef.markForCheck();
  }

  addNewExternalSource() {
    this.externalSources.unshift({id: 0, name: '', host: '', apiKey: ''});
    this.cdRef.markForCheck();
  }

  updateSource(index: number, updatedSource: ExternalSource) {
    this.externalSources[index] = updatedSource;
    this.cdRef.markForCheck();
  }

  deleteSource(index: number, updatedSource: ExternalSource) {
    this.externalSources.splice(index, 1);
    this.resetFilter();
    this.cdRef.markForCheck();
  }


}
