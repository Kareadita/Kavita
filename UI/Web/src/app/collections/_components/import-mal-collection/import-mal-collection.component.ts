import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
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
import {EmptyStateComponent} from "../../../shared/_components/empty-state/empty-state.component";

@Component({
    selector: 'app-import-mal-collection',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    DecimalPipe,
    EmptyStateComponent
  ],
    templateUrl: './import-mal-collection.component.html',
    styleUrl: './import-mal-collection.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportMalCollectionComponent {
  private readonly collectionService = inject(CollectionTagService);
  private readonly toastr = inject(ToastrService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly confirmService = inject(ConfirmService);

  stacks = signal<MalStack[]>([]);
  isLoading = signal<boolean>(true);
  collectionMap= signal<{[key: string]: UserCollection | MalStack}>({});

  constructor() {
    this.scrobblingService.getScrobbleProviders().subscribe(async res => {
      const potentialMal = res.filter(r => r.provider === ScrobbleProvider.Mal);
      if (potentialMal.length === 0 || potentialMal[0].authenticationToken === '') {
        this.isLoading.set(false);
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
        this.collectionMap.update(m => {
          m[col.sourceUrl!] = col;
          return {...m};
        });
      }

      this.stacks.set(res.malStacks);
      this.isLoading.set(false);
    });
  }

  importStack(stack: MalStack) {
    this.collectionService.importStack(stack).subscribe(() => {
      this.collectionMap.update(m => {
        m[stack.url] = stack;
        return {...m};
      });
      this.toastr.success(translate('toasts.stack-imported'));
    })
  }
}
