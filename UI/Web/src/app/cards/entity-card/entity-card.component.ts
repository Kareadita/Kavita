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
import {
  ActionableCardConfiguration,
  BaseCardConfiguration,
  hasActionables
} from "../../_models/card/card-configuration";
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
import {IHasProgress} from "../../_models/common/i-has-progress";
import {ThemeService} from "../../_services/theme.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {ActionResult} from "../../_models/actionables/action-result";

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
export class EntityCardComponent<T> implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scrollService = inject(ScrollService);
  private readonly themeService = inject(ThemeService);
  protected readonly imageService = inject(ImageService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);

  // ============================================================
  // INPUTS
  // ============================================================

  /** The wrapped entity containing type discriminator and data */
  entity = input.required<CardEntity>();

  /** Configuration defining how the card renders and behaves */
  config = input.required<BaseCardConfiguration<T>>();

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

  /** Router link for title */
  protected titleRouteParams: Signal<Record<string, any>> = computed(() =>
    this.config().titleRouteParamsFunc?.(this.data()) ?? {}
  );

  /** Meta title text (fallback when no template) */
  protected metaTitle: Signal<string> = computed(() =>
    this.config().metaTitleFunc(this.data(), this.entity()) ?? ''
  );

  /** Tooltip text */
  protected tooltip: Signal<string> = computed(() =>
    this.config().tooltipFunc(this.data())
  );

  /** Reading progress */
  protected progress: Signal<IHasProgress> = computed(() =>
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
  protected isSelected: Signal<boolean> = computed(() => {
    this.bulkSelectionService.selectionSignal(); // Ensure we re-render when deselect occurs
    return this.config().allowSelection &&
      this.bulkSelectionService.isCardSelected(this.config().selectionType, this.index());
  });

  /** Whether action menu should display */
  protected hasActionables: Signal<boolean> = computed(() =>
    this.actionables().length > 0
  );

  protected actionables: Signal<ActionItem<any>[]> = computed(() => {
    const config = this.config();
    const data = this.data();

    if (hasActionables(config) && data) {
      // Cast to a generic ActionableCardConfiguration to bridge the T gap
      const actionableCfg = config as unknown as ActionableCardConfiguration<ActionableEntity>;
      return actionableCfg.actionableFunc(data as unknown as ActionableEntity);
    }

    return [];
  });

  protected actionableEntity: Signal<ActionableEntity | null> = computed(() => {
    return this.hasActionables()
      ? (this.data() as unknown as ActionableEntity)
      : null;
  });

  /** Aria label for accessibility */
  protected ariaLabel: Signal<string> = computed(() =>
    this.config().ariaLabelFunc?.(this.data()) ?? this.title()
  );

  protected cardWidth = computed(() => {
    return this.themeService.getCssVariable('--card-image-width');
  });

  protected cardHeight = computed(() => {
    return this.themeService.getCssVariable('--card-image-height');
  });

  protected download$: Observable<DownloadEvent | null> | null = null;

  private prevTouchTime = 0;
  private prevOffset = 0;
  private selectionInProgress = false;

  ngOnInit() {
    this.setupDownloadTracking();
  }

  private setupDownloadTracking() {
    const downloadFunc = this.config().downloadObservableFunc;
    if (downloadFunc) {
      this.download$ = downloadFunc(this.data()).pipe(
        takeUntilDestroyed(this.destroyRef)
      );
    }
  }

  @HostListener('touchmove')
  onTouchMove() {
    if (!this.config().allowSelection) return;
    this.selectionInProgress = false;
    this.cdRef.markForCheck();
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    if (!this.config().allowSelection) return;
    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
    this.selectionInProgress = true;
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
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

  handleClick(event?: Event) {
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

  handleSelection(event?: Event) {
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

  onActionResult(event: ActionItem<any> | ActionResult<any>) {
    if (!('effect' in event)) return; // Ignore legacy ActionItem events
    const result = event as ActionResult<any>;
    switch (result.effect) {
      case 'update':
        this.dataChanged.emit(result.entity);
        break;
      case 'remove':
      case 'reload':
        this.reload.emit(result.entity?.id ?? 0);
        break;
      case 'none':
        break;
    }
  }

  handleRead(event: Event) {
    event.stopPropagation();

    // Don't trigger read if in bulk selection mode
    if (this.bulkSelectionService.hasSelections()) return;

    this.config().readFunc(this.data());
  }

  /** Check if meta title is not empty/null **/
  get shouldRenderMetaTitle() {
    return !!this.metaTitle() || this.hasMetaTitleTemplate;
  }

  /** Check if meta title template is provided */
  get hasMetaTitleTemplate() {
    return !!this.config().metaTitleTemplate;
  }

  get hasTitleTemplate() {
    return !!this.config().titleTemplate;
  }

  get metaTitleTemplate() {
    return this.config().metaTitleTemplate;
  }

  get titleTemplate() {
    return this.config().titleTemplate;
  }

  /** Get entity ID for accessibility attributes */
  get entityId() {
    const data = this.data() as { id?: number };
    return data.id ?? 0;
  }
}
