import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {TranslocoDirective} from "@ngneat/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {MalStack} from "../../../_models/collection/mal-stack";

@Component({
  selector: 'app-import-mal-collection-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    Select2Module
  ],
  templateUrl: './import-mal-collection-modal.component.html',
  styleUrl: './import-mal-collection-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportMalCollectionModalComponent {

  protected readonly ngbModal = inject(NgbActiveModal);
  protected readonly collectionService = inject(CollectionTagService);
  protected readonly cdRef = inject(ChangeDetectorRef);

  stacks: Array<MalStack> = [];
  isLoading = true;

  constructor() {
    this.collectionService.getMalStacks().subscribe(stacks => {
      this.stacks = stacks;
      this.isLoading = false;
      this.cdRef.markForCheck();
    })

  }


}
