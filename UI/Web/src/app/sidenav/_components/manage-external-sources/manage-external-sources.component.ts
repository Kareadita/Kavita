import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgbCollapse, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {EditExternalSourceItemComponent} from "../edit-external-source-item/edit-external-source-item.component";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {ExternalSourceService} from "../../../external-source.service";

@Component({
  selector: 'app-manage-external-sources',
  standalone: true,
  imports: [CommonModule, FormsModule, NgOptimizedImage, NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgbCollapse, EditExternalSourceItemComponent],
  templateUrl: './manage-external-sources.component.html',
  styleUrls: ['./manage-external-sources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageExternalSourcesComponent {

  externalSources: Array<ExternalSource> = [];
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly externalSourceService = inject(ExternalSourceService);


  constructor(public accountService: AccountService) {
    this.externalSourceService.getExternalSources().subscribe(data => {
      this.externalSources = data;
      this.cdRef.markForCheck();
    });
  }

  addNewExternalSource() {
    this.externalSources.unshift({id: 0, host: '', apiKey: ''});
    this.cdRef.markForCheck();
  }

  updateSource(index: number, updatedSource: ExternalSource) {
    this.externalSources[index] = updatedSource;
    this.cdRef.markForCheck();
  }

  deleteSource(index: number, updatedSource: ExternalSource) {
    this.externalSources.splice(index, 1);
    this.cdRef.markForCheck();
  }


}
