import {
  ChangeDetectionStrategy,
  Component, computed,
  DestroyRef, effect,
  EventEmitter,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {
  SideNavCompanionBarComponent
} from "../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {ActivatedRoute, Router} from "@angular/router";
import {AnnotationService} from "../../_services/annotation.service";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {Annotation} from "../../book-reader/_models/annotations/annotation";
import {Pagination} from "../../_models/pagination";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map, tap} from "rxjs/operators";
import {AnnotationsFilterSettings} from "../../metadata-filter/filter-settings";
import {AnnotationsFilter} from "../../_models/metadata/v2/filter-v2";
import {AnnotationsFilterField, AnnotationsSortField} from "../../_models/metadata/v2/annotations-filter";
import {MetadataService} from "../../_services/metadata.service";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";
import {FilterEvent} from "../../_models/metadata/series-filter";
import {DecimalPipe} from "@angular/common";
import {CardDetailLayoutComponent} from "../../cards/card-detail-layout/card-detail-layout.component";
import {
  AnnotationCardComponent
} from "../../book-reader/_components/_annotations/annotation-card/annotation-card.component";

@Component({
  selector: 'app-browse-annotations',
  imports: [
    SideNavCompanionBarComponent,
    TranslocoDirective,
    DecimalPipe,
    CardDetailLayoutComponent,
    AnnotationCardComponent
  ],
  templateUrl: './browse-annotations.component.html',
  styleUrl: './browse-annotations.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrowseAnnotationsComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly annotationsService = inject(AnnotationService);
  private readonly route = inject(ActivatedRoute);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly metadataService = inject(MetadataService);


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

  constructor() {
    effect(() => {
      const event = this.annotationsService.events();
      if (!event) return;

      switch (event.type) {
        case "delete":
          this.annotations.update(x => x.filter(a => a.id !== event.annotation.id));
      }
    });
  }

  ngOnInit() {
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
