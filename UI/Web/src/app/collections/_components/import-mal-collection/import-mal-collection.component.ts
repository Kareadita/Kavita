import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CollectionTagService} from "../../../_services/collection-tag.service";
import {ToastrService} from "ngx-toastr";
import {ScrobbleProvider, ScrobblingService} from "../../../_services/scrobbling.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {MalStack} from "../../../_models/collection/mal-stack";
import {UserCollection} from "../../../_models/collection-tag";
import {forkJoin} from "rxjs";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {DecimalPipe} from "@angular/common";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";

@Component({
  selector: 'app-import-mal-collection',
  standalone: true,
  imports: [
    TranslocoDirective,
    LoadingComponent,
    DecimalPipe,
    DefaultValuePipe
  ],
  templateUrl: './import-mal-collection.component.html',
  styleUrl: './import-mal-collection.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportMalCollectionComponent {
  private readonly collectionService = inject(CollectionTagService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly confirmService = inject(ConfirmService);

  stacks: Array<MalStack> = [];
  isLoading = true;
  collectionMap: {[key: string]: UserCollection | MalStack} = {};

  constructor() {
    this.scrobblingService.getMalToken().subscribe(async token => {
      if (token.accessToken === '') {
        await this.confirmService.alert(translate('toasts.mal-token-required'));
        return;
      }
      this.setup();
    });
  }

  setup() {
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
