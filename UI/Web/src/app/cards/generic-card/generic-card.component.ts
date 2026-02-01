import {ChangeDetectionStrategy, Component, computed, inject, input, Signal} from '@angular/core';
import {AbstractGenericCard, CardConfiguration} from "../AbstractGenericCard";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {RouterLink} from "@angular/router";
import {IHasProgress} from "../../_models/common/i-has-progress";
import {TranslocoDirective} from "@jsverse/transloco";
import {DownloadIndicatorComponent} from "../download-indicator/download-indicator.component";
import {ImageComponent} from "../../shared/image/image.component";
import {BulkSelectionService} from "../bulk-selection.service";
import {DecimalPipe} from "@angular/common";
import {ImageService} from "../../_services/image.service";
import {FormsModule} from "@angular/forms";
import {RelationshipPipe} from "../../_pipes/relationship.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";


@Component({
  selector: 'app-generic-card',
  imports: [
    CardActionablesComponent,
    SeriesFormatComponent,
    NgbTooltip,
    RouterLink,
    TranslocoDirective,
    DownloadIndicatorComponent,
    ImageComponent,
    NgbProgressbar,
    DecimalPipe,
    FormsModule,
    RelationshipPipe,
    DefaultValuePipe
  ],
  templateUrl: './generic-card.component.html',
  styleUrl: './generic-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GenericCardComponent<T> extends AbstractGenericCard<T> {
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly imageService = inject(ImageService);

  config = input.required<CardConfiguration<T>>();
  entity = input.required<any>();
  /**
   * A badge to display information
   */
  count = input<number>(0);

  progress: Signal<IHasProgress> = computed(() => {
    const entity = this.entity();
    if (this.hasProgress(entity)) {
      return this.config().progressFunc(entity);
    }
    return {pages: 0, pagesRead: 0};
  });

  isSelected = computed(() => {
    return this.config().allowSelection && this.bulkSelectionService.isCardSelected(this.config().selectionType, this.index())
  });

  hasActionables = computed(() => {
    return this.config().actionables.length > 0;
  })

  downloadUrl: Signal<string> = computed(() => this.config().downloadFunc(this.entity()));

  private hasProgress(entity: T): entity is T & IHasProgress {
    return 'pagesRead' in (entity as object) && 'pages' in (entity as object);
  }

  handleClick() {

  }

  handleSelection(event: any) {
    if (event) {
      event.stopPropagation();
    }

    this.bulkSelectionService.handleCardSelection(this.config().selectionType, this.index(), this.maxIndex(), this.isSelected());
  }

}
