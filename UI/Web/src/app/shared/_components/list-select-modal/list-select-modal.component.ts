import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  TemplateRef,
  untracked,
  viewChild
} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {toSignal} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../loading/loading.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";

export type ListSelectionItem<T> = {
  label: string,
  value: T,
}


@Component({
  selector: 'app-list-select-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SentenceCasePipe,
    NgTemplateOutlet,
    LoadingComponent,
    VirtualScrollerModule
  ],
  templateUrl: './list-select-modal.component.html',
  styleUrl: './list-select-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ListSelectModalComponent<T> {

  private readonly modal = inject(NgbActiveModal);

  defaultTemplate = viewChild.required<TemplateRef<any>>('defaultTemplate');

  title = model.required<string>();
  description = model<string | null>(null);
  selectedText = model(translate('common.selected'));
  invalidSelectionWarning = model<string | null>(null);
  hiddenTranslationKey = model('list-select-modal.hidden')

  inputItems = model.required<ListSelectionItem<T>[]>();
  preSelectedItems = model<T[]>([]);
  isValidItemFunc = model<(item: T, selection: T[]) => boolean>(() => true);
  isSelectionValidFunc = model<(selection:T[]) => boolean>(() => true);
  interceptConfirm = model<((selection: T|T[]) => void) | null>(null);

  itemsBeforeFilter = model(8);
  itemsBeforeVirtual = model<number | null>(null);
  requireConfirmation = model(false);
  showFooter = model(true);
  showConfirm = model(true);
  multiSelect = model(false);
  hideItemsWhenInvalid = model(false);

  itemTemplate = model<TemplateRef<any> | null>(null);

  loading = model(false);

  protected validSelection = computed(() => {
    const fn = this.isSelectionValidFunc();
    const selection = this.selectedItems().map(item => item.value);

    return fn(selection);
  })

  protected finalItemTemplate = computed(() => {
    const defaultTemplate = this.defaultTemplate();
    const itemTemplate = this.itemTemplate();

    if (itemTemplate) {
      return itemTemplate;
    }

    return defaultTemplate;
  })

  protected selectedItems = signal<ListSelectionItem<T>[]>([]);

  protected items = computed(() => {
    const items = this.inputItems();
    const hideOnInvalid = this.hideItemsWhenInvalid();

    if (!hideOnInvalid) return items;

    return items.filter(item => this.isItemValid(item));
  });

  protected hiddenItems = computed(() => {
    const allItems = this.inputItems();
    const items = this.items();

    return allItems.length - items.length;
  });

  protected filteredItems = computed(() => {
    const items = this.items();
    const filter = (this.filterQuery() ?? '').toLowerCase();

    if (!filter) return items;

    return items.filter(item => item.label.toLowerCase().includes(filter));
  });

  protected filterForm = new FormGroup({
    query: new FormControl('', {nonNullable: true}),
  });
  protected filterQuery = toSignal(this.filterForm.get('query')!.valueChanges, {initialValue: ''});

  constructor() {
    effect(() => {
      const items = this.inputItems();
      const preSelectedItems = this.preSelectedItems();
      const selectedItems = untracked(this.selectedItems); // Don't trigger effect when selected items changes

      // Never overwrite selected items
      if (selectedItems.length > 0) return;

      this.selectedItems.set(items.filter(item => preSelectedItems.includes(item.value)));
    });
  }

  isItemValid(item: ListSelectionItem<T>) {
    // Assume selected items are always valid
    if (this.selectedItems().includes(item)) return true;


    return this.isValidItemFunc()(item.value, this.selectedItems().map(item => item.value));
  }

  select(item: ListSelectionItem<T>) {
    if (!this.isItemValid(item)) return;

    if (this.multiSelect()) {
      const currentlySelected = this.selectedItems().includes(item);
      if (currentlySelected) {
        this.selectedItems.update(x => [...x.filter(i => i !== item)])
      } else {
        this.selectedItems.update(x => [...x, item])
      }


    } else {
      this.selectedItems.set([item]);
    }

    if (!this.requireConfirmation() && !this.multiSelect()) {
      this.confirm();
      return;
    }
  }

  clear() {
    this.filterForm.get('query')?.setValue('');
  }

  close() {
    this.modal.dismiss();
  }

  confirm() {
    const intercept = this.interceptConfirm();
    const fn = intercept == null ? this.modal.close : intercept;


    if (this.multiSelect()) {
      fn(this.selectedItems().map(i => i.value))
    } else {
      fn(this.selectedItems()[0].value);
    }
  }

}
