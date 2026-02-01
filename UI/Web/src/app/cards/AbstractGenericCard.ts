import {computed, Directive, input, Signal} from "@angular/core";
import {IHasProgress} from "../_models/common/i-has-progress";
import {ActionItem} from "../_services/action-factory.service";
import {BulkSelectionEntityDataSource} from "./bulk-selection.service";


export interface CardConfiguration<T> {
  allowSelection: boolean;
  selectionType: BulkSelectionEntityDataSource;
  readFunc: (entity: T) => void;
  titleFunc: (entity: T) => string;
  titleRoute: (entity: T) => string;
  tooltipFunc: (entity: T) => string;
  hoverTitleFunc: (entity: T) => string;
  actionables: ActionItem<T>[];
  coverFunc: (entity: T) => string;
  progressFunc: (entity: T & IHasProgress) => IHasProgress;
  downloadFunc: (entity: T) => string;
}


@Directive()
export abstract class AbstractGenericCard<T> {
  config = input.required<CardConfiguration<T>>();
  entity = input.required<T>();
  /**
   * Index in the rendered for loop. This will drive bulk selection if applicable
   */
  index = input<number>(0);
  /**
   * Total Items for the rendered for loop. This will drive bulk selection if applicable
   */
  maxIndex = input<number>(1);

  title: Signal<string> = computed(() => this.config().titleFunc(this.entity()));
  tooltip: Signal<string> = computed(() => this.config().tooltipFunc(this.entity()));
  hoverTitle: Signal<string> = computed(() => this.config().hoverTitleFunc(this.entity()));
  coverUrl: Signal<string> = computed(() => this.config().coverFunc(this.entity()));
  routerLink: Signal<string> = computed(() => this.config().titleRoute(this.entity()));

}
