import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  contentChild,
  CUSTOM_ELEMENTS_SCHEMA, DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  model,
  output,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {Swiper} from 'swiper/types';
import {NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";
import {map, Observable, tap} from "rxjs";
import {PaginatedResult} from "../../../_models/pagination";
import {ActionItem} from "../../../_models/actionables/action-item";
import {ActionResult} from "../../../_models/actionables/action-result";
import {ActionableEntity} from "../../../_services/action-factory.service";
import {register} from "swiper/element/bundle";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

register();

export type NextPageLoader<T> = (pageNumber: number, pageSize: number) => Observable<T[] | PaginatedResult<T[]>>;

@Component({
  selector: 'app-carousel-reel',
  templateUrl: './carousel-reel.component.html',
  styleUrls: ['./carousel-reel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, TranslocoDirective, CardActionablesComponent, SafeUrlPipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class CarouselReelComponent<T> {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly carouselItemTemplate = contentChild.required<TemplateRef<never>>('carouselItem');
  readonly promptToAddTemplate = contentChild.required<TemplateRef<never>>('promptToAdd');
  readonly noDataTemplate = contentChild<TemplateRef<never>>('noData');

  readonly swiperContainer = viewChild<ElementRef<HTMLElement & {swiper: Swiper}>>('swiperContainer');

  readonly isBeginning = signal(true);
  readonly isEnd = signal(false);

  items = model<T[]>([]);
  title = input<string>('');
  /**
   * If provided, will render the title as an anchor
   */
  titleLink = input<string>('');
  clickableTitle = input<boolean>(true);
  iconClasses = input<string>('');
  /**
   * Show's the carousel component even if there is nothing in it
   */
  alwaysShow = input<boolean>(false);
  /**
   * Track by identity. By default, this has an implementation based on title, item's name, pagesRead, and index
   */
  trackByIdentity = input<(index: number, item: T) => string>((index: number, item: any) => {
    return `${this.title()}_${item.id}_${item?.name}_${item?.pagesRead}_${index}`;
  });
  /**
   * Actionables to render to the left of the title
   */
  actionables = input<ActionItem<T>[]>([]);
  /**
   * If using actionables, this is the entity to allow Action.Service to handle logic
   */
  actionableEntity = input<ActionableEntity | null>(null);
  headerClass = input<string>('section-title');

  readonly sectionClick = output<string>();
  readonly handleAction = output<ActionItem<T>>();

  readonly actionHandler = output<ActionResult<T>>();

  currentPage = signal<number>(1);
  pageSize = input(20);
  nextPageLoader = input<NextPageLoader<T> | null>(null);

  paginationEnabled = computed(() => this.nextPageLoader() != null);
  loadingNextPage = signal(false);
  totalPages = signal<number>(999_999_999_999);

  swiper = signal<Swiper | undefined>(undefined);

  isNextDisabled = computed(() => {
    return this.isEnd()
    && (!this.paginationEnabled() || this.items().length < this.pageSize())
    || (this.currentPage() >= (this.totalPages()));
  });

  constructor() {
    // element's connectedCallback -> initialize() sets .swiper synchronously (this avoids binding to swiperprogress like docs suggest and incurring lag on each scroll tick)
    effect(() => {
      const swiper = this.swiperContainer()?.nativeElement?.swiper;
      this.swiper.set(swiper);
      this.syncEdges(swiper);
    });
  }

  syncEdges(s: Swiper | undefined = this.swiper()) {
    this.isBeginning.set(s?.isBeginning ?? true);
    this.isEnd.set(s?.isEnd ?? false);

    // On first load (no items loaded) isBeginning & isEnd may be true at the same time
    if (!this.isBeginning() && this.isEnd() && s?.initialized) {
      this.tryLoadNextPage();
    }
  }

  private tryLoadNextPage() {
    if (!this.paginationEnabled() || this.loadingNextPage() || this.currentPage() >= this.totalPages()) {
      return;
    }

    this.currentPage.update(x => x + 1);
    this.loadingNextPage.set(true);
    const oldSize = this.items().length;

    this.nextPageLoader()!(this.currentPage(), this.pageSize()).pipe(
      takeUntilDestroyed(this.destroyRef),
      map(items => {
        if (Array.isArray(items)) {
          return items;
        }

        const pagedList = items as PaginatedResult<T[]>;
        this.totalPages.set(pagedList.pagination.totalPages)

        return pagedList.result;
      }),
      tap(items => {
        this.items.set([...this.items(), ...items]);

        const newCurrentProgress = oldSize / this.items().length;
        this.swiper()?.setProgress(newCurrentProgress);
        this.cdRef.markForCheck();
      }),
      //tap(() => this.nextPage()),
      tap(() => this.loadingNextPage.set(false)),
    ).subscribe();
  }

  get progressChange() {
    const totalItems = this.items().length;
    const itemsToMove = Math.min(5, totalItems);
    const progressPerItem = 1 / totalItems;
    return Math.min(0.25, progressPerItem * itemsToMove);
  }

  nextPage() {
    const swiper = this.swiper();
    if (swiper) {
      if (swiper.isEnd) {
        this.tryLoadNextPage();
        return;
      }

      swiper.setProgress(swiper.progress + this.progressChange, 600);
      this.cdRef.markForCheck();
    }
  }

  prevPage() {
    const swiper = this.swiper();
    if (swiper) {
      if (swiper.isBeginning) return;
      swiper.setProgress(swiper.progress - this.progressChange, 600);
      this.cdRef.markForCheck();
    }
  }

  sectionClicked() {
    this.sectionClick.emit(this.title());
  }

  performAction(event: ActionResult<T>) {
    this.actionHandler.emit(event);
  }
}
