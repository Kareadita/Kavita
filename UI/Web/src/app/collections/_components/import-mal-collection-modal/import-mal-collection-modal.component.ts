import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {MalStack} from "../../../_models/collection/mal-stack";
import {UserCollection} from "../../../_models/collection-tag";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {forkJoin} from "rxjs";
import {ToastrService} from "ngx-toastr";
import {DecimalPipe} from "@angular/common";
import {LoadingComponent} from "../../../shared/loading/loading.component";

@Component({
  selector: 'app-import-mal-collection-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    Select2Module,
    DecimalPipe,
    LoadingComponent
  ],
  templateUrl: './import-mal-collection-modal.component.html',
  styleUrl: './import-mal-collection-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportMalCollectionModalComponent {

  protected readonly ngbModal = inject(NgbActiveModal);
  private readonly collectionService = inject(CollectionTagService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);

  stacks: Array<MalStack> = [];
  isLoading = true;
  collectionMap: {[key: string]: UserCollection | MalStack} = {};

  constructor() {

    forkJoin({
      allCollections: this.collectionService.allCollections(true),
      malStacks: this.collectionService.getMalStacks()
    }).subscribe(res => {

      // Create a map on sourceUrl from collections so that if there are non-null sourceUrl (and source is MAL) then we can disable buttons
      const collects = res.allCollections.filter(c => c.source === ScrobbleProvider.Mal && c.sourceUrl);
      for(let col of collects) {
        if (col.sourceUrl === null) continue;
        this.collectionMap[col.sourceUrl] = col;
      }

      this.stacks = res.malStacks;
      this.isLoading = false;

      this.cdRef.markForCheck();
    });
  }

  importStack(stack: MalStack) {
    this.collectionService.importStack(stack).subscribe(() => {
      this.collectionMap[stack.url] = stack;
      this.cdRef.markForCheck();
      this.toastr.success(translate('toasts.stack-imported'));
    })
  }


}
