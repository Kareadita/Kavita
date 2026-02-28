import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  model,
  signal,
  TemplateRef,
  untracked,
  viewChild
} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {toSignal} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../loading/loading.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {Observable} from "rxjs";
import {modalSaved} from "../../../_models/modal/modal-result";

/**
 * A single selectable item in the list.
 * `label` is displayed in the UI; `value` is what the modal emits on confirm.
 */
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

  /** The modal title shown in the header. */
  title = model.required<string>();
  /** Optional subtitle displayed below the header in muted text. */
  description = model<string | null>(null);
  selectedText = model(translate('common.selected'));
  invalidSelectionWarning = model<string | null>(null);
  hiddenTranslationKey = model('list-select-modal.hidden')

  /**
   * The full item dataset. The modal derives `filteredItems` from this automatically.
   * Update this signal at any time (e.g. after async loading) to refresh the list.
   */
  inputItems = model.required<ListSelectionItem<T>[]>();
  /**
   * Items pre-marked as selected when the modal first opens.
   * Ignored once the user has made any selection.
   */
  preSelectedItems = model<T[]>([]);
  /**
   * Per-item validity check. Receives the candidate item value and the current selection.
   * Returning false disables the row (grayed out; not clickable).
   */
  isValidItemFunc = model<(item: T, selection: T[]) => boolean>(() => true);
  /**
   * Whole-selection validity check called before `confirm()`.
   * Returning false disables the Confirm button.
   */
  isSelectionValidFunc = model<(selection:T[]) => boolean>(() => true);
  /**
   * If provided, called instead of `modal.close()` when the user confirms.
   * Receives a single value in single-select mode or an array in multi-select mode.
   */
  interceptConfirm = model<((selection: T|T[]) => void) | null>(null);

  /** Minimum list length before the filter input is shown. Default: 8. */
  itemsBeforeFilter = model(8);
  /**
   * Threshold above which the list switches to virtual scrolling.
   * Null (default) disables virtual scrolling.
   */
  itemsBeforeVirtual = model<number | null>(null);
  /** Require an explicit Confirm button click instead of closing on item select. */
  requireConfirmation = model(false);
  /** Show the modal footer row. Set false to suppress Close/Confirm entirely. */
  showFooter = model(true);
  /** Show the Confirm button inside the footer. */
  showConfirm = model(true);
  /** Allow selecting multiple items; `confirm()` emits T[] instead of T. */
  multiSelect = model(false);
  /**
   * When true, items that fail `isValidItemFunc` are hidden rather than disabled.
   * Use `invalidSelectionWarning` and `hiddenTranslationKey` to inform the user.
   */
  hideItemsWhenInvalid = model(false);

  /** Custom ng-template for rendering each list row. Receives `$implicit: ListSelectionItem<T>`. */
  itemTemplate = model<TemplateRef<any> | null>(null);

  /** Externally controlled loading state; shows a spinner overlay on the list. */
  loading = model(false);

  /** Show a "create new" text-input row in the footer. */
  showCreate = model(false);
  /** Placeholder / label text for the create input. */
  createLabel = model<string | null>(null);
  /** Initial value pre-filled in the create input. */
  createInitialValue = model('');
  /**
   * Override the Create button label. Falls back to t('common.create') when null,
   * allowing callers to pass 'Save' or any translated string without component changes.
   */
  createButtonLabel = model<string | null>(null);
  /**
   * Callback fired when the user clicks Create, receiving the typed name.
   * Must return an Observable. The component subscribes and:
   *   - sets isCreating = true before calling
   *   - resets isCreating = false on complete OR error
   *   - calls modal.close() on complete (keeping modal open on error for retry)
   * If null, the modal closes immediately with modalSaved(name).
   */
  interceptCreate = model<((name: string) => Observable<unknown>) | null>(null);

  protected readonly isCreating = signal(false);
  protected readonly createControl = new FormControl('', { nonNullable: true, validators: Validators.required });

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

    effect(() => {
      this.createControl.setValue(this.createInitialValue());
    });
  }

  isItemValid(item: ListSelectionItem<T>) {
    // Assume selected items are always valid
    if (this.selectedItems().includes(item)) return true;


    return this.isValidItemFunc()(item.value, this.selectedItems().map(item => item.value));
  }

  /** Toggles or sets the selected item, then auto-confirms in single-select non-confirmation mode. */
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

  /** Resets the filter input. */
  clear() {
    this.filterForm.get('query')?.setValue('');
  }

  /** Dismisses the modal without a result. */
  close() {
    this.modal.dismiss();
  }

  /** Confirms the current selection, delegating to interceptConfirm if set. */
  confirm() {
    const intercept = this.interceptConfirm();
    const fn = intercept == null ? this.modal.close : intercept;


    if (this.multiSelect()) {
      fn(this.selectedItems().map(i => i.value))
    } else {
      fn(this.selectedItems()[0].value);
    }
  }

  /**
   * Handles the Create button click.
   * If interceptCreate is set, subscribes to the returned Observable and closes on complete.
   * Otherwise closes immediately with modalSaved(name) so the caller can handle the API call.
   */
  create() {
    const name = this.createControl.value;
    const intercept = this.interceptCreate();
    if (!intercept) {
      this.modal.close(modalSaved(name));
      return;
    }
    this.isCreating.set(true);
    intercept(name).subscribe({
      complete: () => { this.isCreating.set(false); this.modal.close(modalSaved(name)); },
      error:    () => { this.isCreating.set(false); }
    });
  }

}
