import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgbCollapse, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {AccountService} from "../../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {EditExternalSourceItemComponent} from "../edit-external-source-item/edit-external-source-item.component";
import {ExternalSource} from "../../../_models/sidenav/external-source";

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
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);


  constructor(public accountService: AccountService) {

  }


}
