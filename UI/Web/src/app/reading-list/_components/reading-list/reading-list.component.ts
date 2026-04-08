import {ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, inject, input, output} from '@angular/core';
import {RouterLink} from '@angular/router';
import {FormsModule} from '@angular/forms';
import {TranslocoDirective} from '@jsverse/transloco';
import {ReadingList} from 'src/app/_models/reading-list';
import {ReadingListCardEntity} from 'src/app/_models/card/card-entity';
import {ActionableCardConfiguration, hasActionables} from 'src/app/_models/card/card-configuration';
import {ActionItem} from 'src/app/_models/actionables/action-item';
import {ActionResult} from 'src/app/_models/actionables/action-result';
import {ActionableEntity} from 'src/app/_services/action-factory.service';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {BreakpointService} from 'src/app/_services/breakpoint.service';
import {ImageComponent} from 'src/app/shared/image/image.component';
import {PromotedIconComponent} from 'src/app/shared/_components/promoted-icon/promoted-icon.component';
import {CardActionablesComponent} from 'src/app/_single-module/card-actionables/card-actionables.component';
import {DateYearRangePipe} from 'src/app/_pipes/date-year-range.pipe';
import {ScrollService} from 'src/app/_services/scroll.service';

@Component({
  selector: 'app-reading-list',
  templateUrl: './reading-list.component.html',
  styleUrls: ['./reading-list.component.scss'],
  imports: [
    RouterLink,
    FormsModule,
    TranslocoDirective,
    ImageComponent,
    PromotedIconComponent,
    CardActionablesComponent,
    DateYearRangePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadingListComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrollService = inject(ScrollService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly breakpointService = inject(BreakpointService);

  entity = input.required<ReadingListCardEntity>();
  config = input.required<ActionableCardConfiguration<ReadingList>>();
  index = input<number>(0);
  maxIndex = input<number>(1);

  readonly reload = output<number>();
  readonly dataChanged = output<ReadingList>();

  protected readonly data = computed(() => this.entity().data);
  protected readonly coverUrl = computed(() => this.config().coverFunc(this.data()));
  protected readonly title = computed(() => this.config().titleFunc(this.data()));
  protected readonly titleRoute = computed(() => this.config().titleRouteFunc(this.data()));
  protected readonly summary = computed(() => this.data().summary);
  protected readonly promoted = computed(() => this.data().promoted);
  protected readonly itemCount = computed(() => this.data().itemCount);

  protected readonly startDate = computed(() => {
    const d = this.data();
    if (!d.startingYear || d.startingYear === 0) return null;
    return new Date(d.startingYear, (d.startingMonth || 1) - 1);
  });

  protected readonly endDate = computed(() => {
    const d = this.data();
    if (!d.endingYear || d.endingYear === 0) return null;
    return new Date(d.endingYear, (d.endingMonth || 1) - 1);
  });

  protected readonly actionables = computed(() => {
    const config = this.config();
    const data = this.data();
    if (hasActionables(config) && data) {
      const actionableCfg = config as unknown as ActionableCardConfiguration<ActionableEntity>;
      return actionableCfg.actionableFunc(data as unknown as ActionableEntity);
    }
    return [];
  });

  protected readonly actionableEntity = computed(() => {
    return this.actionables().length > 0
      ? (this.data() as unknown as ActionableEntity)
      : null;
  });

  protected readonly isSelected = computed(() => {
    this.bulkSelectionService.selectionSignal();
    return this.config().allowSelection &&
      this.bulkSelectionService.isCardSelected(this.config().selectionType, this.index());
  });

  protected readonly tags = computed(() => {
    // return ['Action', 'Superhero', 'Marvel', 'Crossover', 'Limited Series'] // DEBUG CODE
    return [];
  });

  protected readonly visibleTags = computed(() => {
    const limit = this.breakpointService.isMobile() ? 5 : 10;
    return this.tags().slice(0, limit);
  });

  protected readonly remainingTagCount = computed(() => {
    return this.tags().length - this.visibleTags().length;
  });

  private prevTouchTime = 0;
  private prevOffset = 0;
  private selectionInProgress = false;

  handleSelection(event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    const existingState = this.isSelected();
    this.bulkSelectionService.handleCardSelection(
      this.config().selectionType,
      this.index(),
      this.maxIndex(),
      existingState
    );
    this.cdRef.detectChanges();
  }

  onActionResult(event: ActionItem<any> | ActionResult<any>) {
    if (!('effect' in event)) return;
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

  onTouchStart(event: TouchEvent) {
    if (!this.config().allowSelection) return;
    this.prevTouchTime = event.timeStamp;
    this.prevOffset = this.scrollService.scrollPosition;
    this.selectionInProgress = true;
  }

  onTouchMove() {
    if (!this.config().allowSelection) return;
    this.selectionInProgress = false;
  }

  onTouchEnd(event: TouchEvent) {
    if (!this.config().allowSelection) return;
    const delta = event.timeStamp - this.prevTouchTime;
    const noScroll = this.scrollService.scrollPosition === this.prevOffset;
    const validDuration = delta >= 300 && delta <= 1000;
    if (validDuration && noScroll && this.selectionInProgress) {
      this.handleSelection();
      event.stopPropagation();
      event.preventDefault();
    }
    this.prevTouchTime = 0;
    this.selectionInProgress = false;
  }
}
