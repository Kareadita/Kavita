import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {TranslocoDirective} from "@ngneat/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {MalStack} from "../../../_models/collection/mal-stack";
import {UserCollection} from "../../../_models/collection-tag";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {forkJoin} from "rxjs";

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
  collectionMap!: {[key: string]: UserCollection};

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


}
