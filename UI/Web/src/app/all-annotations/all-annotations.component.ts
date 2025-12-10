import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  EventEmitter,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {ActivatedRoute, Router} from "@angular/router";
import {AnnotationService} from "../_services/annotation.service";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {Pagination} from "../_models/pagination";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map, tap} from "rxjs/operators";
import {AnnotationsFilterSettings} from "../metadata-filter/filter-settings";
import {
  AnnotationsFilter,
  AnnotationsFilterField,
  AnnotationsSortField
} from "../_models/metadata/v2/annotations-filter";
import {MetadataService} from "../_services/metadata.service";
import {FilterStatement} from "../_models/metadata/v2/filter-statement";
import {FilterEvent} from "../_models/metadata/series-filter";
import {DecimalPipe} from "@angular/common";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {
  AnnotationCardComponent
} from "../book-reader/_components/_annotations/annotation-card/annotation-card.component";
import {Action, ActionFactoryService, ActionItem} from "../_services/action-factory.service";
import {BulkOperationsComponent} from "../cards/bulk-operations/bulk-operations.component";
import {BulkSelectionService} from "../cards/bulk-selection.service";
import {User} from "../_models/user/user";
import {AccountService} from "../_services/account.service";

@Component({
  selector: 'app-all-annotations',
  imports: [
    SideNavCompanionBarComponent,
    TranslocoDirective,
    DecimalPipe,
    CardDetailLayoutComponent,
    AnnotationCardComponent,
    BulkOperationsComponent
  ],
  templateUrl: './all-annotations.component.html',
  styleUrl: './all-annotations.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AllAnnotationsComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly annotationsService = inject(AnnotationService);
  private readonly route = inject(ActivatedRoute);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly accountService = inject(AccountService);

  isLoading = signal(true);
  annotations = signal<Annotation[]>([]);
  pagination = signal<Pagination>({
    currentPage: 0,
    itemsPerPage: 0,
    totalItems: 0,
    totalPages: 0
  });
  filterActive = signal(false);
  filter = signal<AnnotationsFilter | undefined>(undefined);

  filterSettings: AnnotationsFilterSettings = new AnnotationsFilterSettings();
  trackByIdentity = (idx: number, item: Annotation) => `${item.id}`;
  refresh: EventEmitter<void> = new EventEmitter();
  filterOpen: EventEmitter<boolean> = new EventEmitter();

  actions: ActionItem<Annotation>[] = [];

  constructor() {
    effect(() => {
      const event = this.annotationsService.events();
      if (!event) return;

      switch (event.type) {
        case "delete":
          this.annotations.update(x => x.filter(a => a.id !== event.annotation.id));
      }
    });

    effect(() => {
      this.annotations();
      this.bulkSelectionService.deselectAll();
    });
  }

  ngOnInit() {
    this.actions = this.actionFactoryService.getAnnotationActions(this.actionFactoryService.dummyCallback);

    this.route.data.pipe(
      takeUntilDestroyed(this.destroyRef),
      map(data => data['filter'] as AnnotationsFilter | null | undefined),
      tap(filter => {
        if (!filter) {
          filter = this.metadataService.createDefaultFilterDto('annotation');
          filter.statements.push(this.metadataService.createDefaultFilterStatement('annotation') as FilterStatement<AnnotationsFilterField>);
        }

        this.filter.set(filter);
        this.filterSettings.presetsV2 = this.filter();
        this.loadData(this.filter())
      }),
    ).subscribe();
  }

  handleAction = async (action: ActionItem<Annotation>, entity: Annotation) => {
    const userId = this.accountService.currentUserSignal()!.id;
    const selectedIndices = this.bulkSelectionService.getSelectedCardsForSource('annotations');
    const selectedAnnotations = this.annotations().filter((_, idx) => selectedIndices.includes(idx+''));
    const ids = selectedAnnotations.map(a => a.id);

    switch (action.action) {
      case Action.Delete:
        this.annotationsService.bulkDelete(ids).pipe(
          tap(() => {
            this.annotations.update(x => x.filter(a => !ids.includes(a.id)));
            this.pagination.update(x => {
              const count = this.annotations().length;

              return {
                ...x,
                totalItems: count,
                totalPages: Math.ceil(count / x.itemsPerPage),
              }
            })
          }),
        ).subscribe();
        break
      case Action.Export:
        this.annotationsService.exportAnnotations(ids).subscribe();
        break
      case Action.Like:
        this.annotationsService.likeAnnotations(ids).pipe(
          tap(() => this.updateLikes(ids, userId, true)),
        ).subscribe();
        break;
      case Action.UnLike:
        this.annotationsService.unLikeAnnotations(ids).pipe(
          tap(() => this.updateLikes(ids, userId, false)),
        ).subscribe();
    }
  }

  private updateLikes(ids: number[], userId: number, like: boolean): void {
    this.annotations.update(annotations =>
      annotations.map(annotation => {
        if (!ids.includes(annotation.id)) return annotation;

        let likes;
        if (like) {
          likes = annotation.likes.includes(userId) ? annotation.likes : [...annotation.likes, userId];
        } else {
          likes = annotation.likes.filter(id => id !== userId);
        }

        return { ...annotation, likes };
      })
    );
  }


  exportFilter() {
    const filter = this.filter();
    if (!filter) return;

    this.annotationsService.exportFilter(filter).subscribe();
  }

  shouldRender = (action: ActionItem<Annotation>, entity: Annotation, user: User) => {
    switch (action.action) {
      case Action.Delete:
        const selectedIndices = this.bulkSelectionService.getSelectedCardsForSource('annotations');
        const selectedAnnotations = this.annotations().filter((_, idx) => selectedIndices.includes(idx+''));
        return selectedAnnotations.find(a => a.ownerUsername !== user.username) === undefined;
    }

    return true;
  }

  private loadData(filter?: AnnotationsFilter) {
    if (!filter) {
      filter = this.metadataService.createDefaultFilterDto('annotation');
      filter.statements.push(this.metadataService.createDefaultFilterStatement('annotation') as FilterStatement<AnnotationsFilterField>);
    }

    this.annotationsService.getAllAnnotationsFiltered(filter).pipe(
      tap(a => {
        this.annotations.set(a.result);
        this.pagination.set(a.pagination);
      }),
      tap(() => this.isLoading.set(false)),
    ).subscribe();
  }

  updateFilter(data: FilterEvent<AnnotationsFilterField, AnnotationsSortField>) {
    if (!data.filterV2) {
      return;
    }

    if (!data.isFirst) {
      this.filterUtilityService.updateUrlFromFilter(data.filterV2).pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(() => this.filter.set(data.filterV2)),
        tap(() => this.loadData(this.filter()))
      ).subscribe();
      return;
    }

    this.filter.set(data.filterV2);
  }
}
