import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  input,
  OnInit,
  Output,
  Signal
} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DownloadEvent} from "../../shared/_services/download.service";
import {Observable} from "rxjs";
import {MangaFormat} from "../../_models/manga-format";
import {CardConfiguration, CardProgress} from "../../_models/card/card-configuration";
import {CardEntity} from "../../_models/card/card-entity";
import {ScrollService} from "../../_services/scroll.service";
import {ImageService} from "../../_services/image.service";
import {BulkSelectionService} from "../bulk-selection.service";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {FormsModule} from "@angular/forms";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {RouterLink} from "@angular/router";
import {DecimalPipe, NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {ActionableEntity} from "../../_services/action-factory.service";

@Component({
  selector: 'app-entity-card',
  imports: [
    ImageComponent,
    NgbProgressbar,
    NgbTooltip,
    DownloadIndicatorComponent,
    FormsModule,
    SeriesFormatComponent,
    RouterLink,
    DecimalPipe,
    TranslocoDirective,
    NgTemplateOutlet,
    CardActionablesComponent
  ],
  templateUrl: './entity-card.component.html',
  styleUrl: './entity-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntityCardComponent<T extends ActionableEntity> implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scrollService = inject(ScrollService);
  protected readonly imageService = inject(ImageService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);

  // ============================================================
  // INPUTS
  // ============================================================

  /** The wrapped entity containing type discriminator and data */
  entity = input.required<CardEntity>();

  /** Configuration defining how the card renders and behaves */
  config = input.required<CardConfiguration<T>>();

  /** Index in the rendered list - drives bulk selection */
  index = input<number>(0);

  /** Total items in the list - drives bulk selection range */
  maxIndex = input<number>(1);

  // ============================================================
  // OUTPUTS
  // ============================================================

  /** Emitted when selection state changes */
  @Output() selection = new EventEmitter<boolean>();

  /** Emitted when card data changes and parent should reload */
  @Output() reload = new EventEmitter<number>();

  /** Emitted when underlying entity data changes */
  @Output() dataChanged = new EventEmitter<T>();

  // ============================================================
  // COMPUTED PROPERTIES
  // ============================================================

  /** Underlying entity data extracted from wrapper */
  protected data: Signal<T> = computed(() => this.entity().data as T);

  /** Cover image URL */
  protected coverUrl: Signal<string> = computed(() =>
    this.config().coverFunc(this.data())
  );

  /** Primary title text */
  protected title: Signal<string> = computed(() =>
    this.config().titleFunc(this.data())
  );

  /** Router link for title */
  protected titleRoute: Signal<string> = computed(() =>
    this.config().titleRouteFunc(this.data())
  );

  /** Meta title text (fallback when no template) */
  protected metaTitle: Signal<string> = computed(() =>
    this.config().metaTitleFunc(this.data(), this.entity())
  );

  /** Tooltip text */
  protected tooltip: Signal<string> = computed(() =>
    this.config().tooltipFunc(this.data())
  );

  /** Reading progress */
  protected progress: Signal<CardProgress> = computed(() =>
    this.config().progressFunc(this.data())
  );

  /** Format badge value (null hides it) */
  protected formatBadge: Signal<MangaFormat | null> = computed(() =>
    this.config().formatBadgeFunc?.(this.data()) ?? null
  );

  /** Count badge value (0 or 1 hides it) */
  protected count: Signal<number> = computed(() =>
    this.config().countFunc?.(this.data()) ?? 0
  );

  /** Whether to show error banner */
  protected showError: Signal<boolean> = computed(() =>
    this.config().showErrorFunc?.(this.data()) ?? this.progress().pages === 0
  );

  /** Whether this card is selected */
  protected isSelected: Signal<boolean> = computed(() =>
    this.config().allowSelection &&
    this.bulkSelectionService.isCardSelected(this.config().selectionType, this.index())
  );

  /** Whether action menu should display */
  protected hasActionables: Signal<boolean> = computed(() =>
    this.config().actionables.length > 0
  );

  /** Aria label for accessibility */
  protected ariaLabel: Signal<string> = computed(() =>
    this.config().ariaLabelFunc?.(this.data()) ?? this.title()
  );

  protected download$: Observable<DownloadEvent | null> | null = null;

  private prevTouchTime = 0;
  private prevOffset = 0;
  private selectionInProgress = false;

  ngOnInit(): void {
    this.setupDownloadTracking();
  }

  private setupDownloadTracking(): void {
    const downloadFunc = this.config().downloadObservableFunc;
    if (downloadFunc) {
      this.download$ = downloadFunc(this.data()).pipe(
        takeUntilDestroyed(this.destroyRef)
      );
    }
  }

  @HostListener('touchmove')
  onTouchMove(): void {
    if (!this.config().allowSelection) return;
    this.selectionInProgress = false;
    this.cdRef.markForCheck();
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent): void {
    if (!this.config().allowSelection) return;
    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
    this.selectionInProgress = true;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent): void {
    if (!this.config().allowSelection) return;

    const delta = event.timeStamp - this.prevTouchTime;
    const verticalOffset = this.scrollService.scrollPosition;
    const noScroll = verticalOffset === this.prevOffset;
    const validDuration = delta >= 300 && delta <= 1000;

    if (validDuration && noScroll && this.selectionInProgress) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }

    this.prevTouchTime = 0;
    this.selectionInProgress = false;
  }

  handleClick(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    // If in bulk selection mode, toggle selection instead of navigating
    if (this.bulkSelectionService.hasSelections()) {
      this.handleSelection();
      return;
    }

    const clickFunc = this.config().clickFunc;
    if (clickFunc) {
      clickFunc(this.data(), this.entity());
    }
  }

  handleSelection(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    this.bulkSelectionService.handleCardSelection(
      this.config().selectionType,
      this.index(),
      this.maxIndex(),
      this.isSelected()
    );

    this.selection.emit(!this.isSelected());
    this.cdRef.detectChanges();
  }

  handleRead(event: Event): void {
    event.stopPropagation();

    // Don't trigger read if in bulk selection mode
    if (this.bulkSelectionService.hasSelections()) return;

    this.config().readFunc(this.data());
  }

  /** Check if meta title template is provided */
  get hasMetaTitleTemplate(): boolean {
    return !!this.config().metaTitleTemplate;
  }

  /** Get the meta title template */
  get metaTitleTemplate() {
    return this.config().metaTitleTemplate;
  }

  /** Get entity ID for accessibility attributes */
  get entityId(): number {
    const data = this.data() as { id?: number };
    return data.id ?? 0;
  }
}
