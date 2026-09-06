import {DOCUMENT, NgClass, NgTemplateOutlet} from '@angular/common';
import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  DestroyRef,
  ElementRef,
  inject,
  input,
  model,
  OnInit,
  output,
  signal,
  Signal,
  TemplateRef,
  viewChild,
  viewChildren
} from '@angular/core';
import {
  CdkConnectedOverlay,
  CdkOverlayOrigin,
  ConnectedPosition,
  ScrollStrategy,
  ScrollStrategyOptions
} from '@angular/cdk/overlay';
import {timer} from 'rxjs';
import {audit, filter, map, switchMap, tap} from 'rxjs/operators';
import {TypeaheadConfig} from '../_models/typeahead-config';
import {takeUntilDestroyed, toObservable, toSignal} from "@angular/core/rxjs-interop";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {SelectionModel} from "../_models/selection-model";
import {KEY_CODES} from "../../shared/_services/utility.service";
import {generateUniqueId} from "../../_helpers/random";
import {form, FormField} from "@angular/forms/signals";

interface FormModel {
  typeahead: string;
}

/**
 * Context handed to the badgeItem and optionItem templates a consumer projects in.
 */
export interface TypeaheadTemplateContext<T> {
  $implicit: T;
  /** Position within the rendered list */
  idx: number;
  /** Current query text, for highlighting the match */
  value: string;
}

@Component({
  selector: 'app-typeahead',
  imports: [TagBadgeComponent, TranslocoDirective, NgTemplateOutlet, NgClass, CdkConnectedOverlay, CdkOverlayOrigin, FormField],
  templateUrl: './typeahead.component.html',
  styleUrls: ['./typeahead.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(body:click)': 'handleDocumentClick($event)',
    '(window:keydown)': 'handleKeyPress($event)'
  }
})
export class TypeaheadComponent<T> implements OnInit {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Settings for the typeahead
   */
  config = input.required<TypeaheadConfig<T>>();
  /**
   * When a field is locked, we render custom css to indicate to the user. Does not affect functionality.
   */
  locked = model<boolean>(false);
  /**
   * If disabled, a user will not be able to interact with the typeahead
   */
  disabled = input<boolean>(false);

  readonly selectedData = output<T[]>();
  readonly selectedItem = output<T | undefined>();
  readonly newItemAdded = output<T>();
  // eslint-disable-next-line @angular-eslint/no-output-on-prefix
  readonly onUnlock = output<void>();


  readonly inputElem = viewChild<ElementRef<HTMLInputElement>>('input');
  readonly optionRows = viewChildren<ElementRef<HTMLElement>>('optionRow');
  readonly triggerEl = viewChild.required<ElementRef<HTMLDivElement>>('triggerEl');
  readonly optionTemplate = contentChild.required<TemplateRef<TypeaheadTemplateContext<T>>>('optionItem');
  readonly badgeTemplate = contentChild.required<TemplateRef<TypeaheadTemplateContext<T>>>('badgeItem');

  protected readonly triggerWidth = signal(0);

  protected readonly overlayPositions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top' },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom' },
  ];
  protected readonly repositionScrollStrategy: ScrollStrategy = inject(ScrollStrategyOptions).reposition();

  useOverlay = computed(() => this.config().dropdownPosition === 'body');

  private optionSelection = new SelectionModel<T>();
  readonly selectedOptions = signal<T[]>([]);

  /** Whether input has active focus */
  hasFocus = signal(false);
  focusedIndex = signal<number>(0);
  showAddItem = signal(false);
  isLoadingOptions = signal(false);
  readonly filteredOptions: Signal<T[]>;

  private readonly formModel = signal<FormModel>({typeahead: ''});
  formGroup = form(this.formModel);

  protected readonly query = computed(() => this.formModel().typeahead);
  protected readonly inputWidth = computed(() => 15 * (this.query().trim().length + 1));
  protected readonly showClearSelection = computed(() =>
    !this.disabled() && this.config().multiple && this.selectedOptions().length > 0);

  // The add-item row occupies index 0 when shown, shifting every option down one
  protected readonly addItemOffset = computed(() => this.showAddItem() ? 1 : 0);
  protected readonly rowCount = computed(() => this.filteredOptions().length + this.addItemOffset());

  private readonly uniqueId = generateUniqueId();
  protected readonly listboxId = `${this.uniqueId}-listbox`;
  protected readonly activeRowId = computed(() => {
    if (!this.hasFocus()) return null;

    const index = this.focusedIndex();
    return index >= 0 && index < this.rowCount() ? this.rowId(index) : null;
  });

  protected rowId(index: number) {
    return `${this.uniqueId}-row-${index}`;
  }



  constructor() {
    afterRenderEffect(() => {
      this.optionRows()[this.focusedIndex()]?.nativeElement.scrollIntoView({block: 'nearest'});
    });

    this.filteredOptions = toSignal(toObservable(this.query)
      .pipe(
        tap(() => this.focusedIndex.set(0)),
        map((val: string) => val.trim()),
        audit(() => timer(this.config().debounce)),
        //distinctUntilChanged(), // ?!: BUG Doesn't trigger the search to run when filtered array changes
        filter((val: string) => {
          // If minimum filter characters not met, do not filter
          if (this.config().minCharacters === 0) return true;

          if (!val || val.length < this.config().minCharacters) {
            return false;
          }

          return true;
        }),
        switchMap((val: string) => {
          this.isLoadingOptions.set(true);
          return this.config().fetchFn(val.trim()).pipe(takeUntilDestroyed(this.destroyRef), map((items: T[]) => items.filter(item => this.filterSelected(item))));
        }),
        tap((filteredOptions: T[]) => {
          this.isLoadingOptions.set(false);
          this.focusedIndex.set(0);
          this.updateShowAddItem(filteredOptions);
        }),
        takeUntilDestroyed(this.destroyRef)
      ), {initialValue: [] as T[]});
  }

  ngOnInit() {
    this.init();
  }

  /**
   * Focuses the input and opens the dropdown. For parents holding a reference via viewChild.
   */
  focusInput() {
    this.onInputFocus();
  }

  /**
   * Closes the dropdown without clearing what is selected.
   */
  blurInput() {
    this.hasFocus.set(false);
  }

  /**
   * Restores the selection to config.savedData, or clears it outright when resetToEmpty is true.
   */
  resetSelections(resetToEmpty: boolean) {
    this.clearSelections(resetToEmpty);
    this.init();
  }

  init() {
    if (this.config().compareFn === undefined && this.config().multiple) {
      console.error('A compare function must be defined');
      return;
    }

    if (this.config().trackByIdentityFn === undefined) {
      console.error('A trackByIdentity function must be defined');
      return;
    }


    this.optionSelection = new SelectionModel<T>(true, this.config().savedData);
    this.syncSelection();
  }


  handleDocumentClick(event: MouseEvent) {
    // Don't close the typeahead when we select an item from it
    if (event.target && (event.target as HTMLElement).classList.contains('list-group-item')) {
      return;
    }
    this.hasFocus.set(false);
  }

  handleKeyPress(event: KeyboardEvent) {
    if (!this.hasFocus()) { return; }
    if (this.disabled()) return;

    switch(event.key) {
      case KEY_CODES.DOWN_ARROW:
      {
        event.preventDefault();
        this.focusedIndex.set(Math.min(this.focusedIndex() + 1, this.rowCount() - 1));
        break;
      }
      case KEY_CODES.UP_ARROW:
      {
        event.preventDefault();
        this.focusedIndex.set(Math.max(this.focusedIndex() - 1, 0));
        break;
      }
      case KEY_CODES.ENTER:
      {
        const index = this.focusedIndex();
        if (index < 0 || index >= this.rowCount()) break;

        event.preventDefault();
        event.stopPropagation();

        if (this.showAddItem() && index === 0) {
          this.addNewItem(this.query());
        } else {
          this.handleOptionClick(this.filteredOptions()[index - this.addItemOffset()]);
        }
        this.focusedIndex.set(0);
        break;
      }
      case KEY_CODES.BACKSPACE:
      case KEY_CODES.DELETE:
      {
        const val = this.formModel().typeahead;
        if (val !== null && val !== undefined && val.trim() !== '') {
          break;
        }
        const selected = [...this.selectedOptions()];
        if (selected.length > 0) {
          const last = selected.pop();
          if (last !== undefined) {
            this.removeSelectedOption(last);
          }
        }
        break;
      }
      case KEY_CODES.TAB:
        this.hasFocus.set(false);
        break;
      case KEY_CODES.ESC_KEY:
        this.hasFocus.set(false);
        event.stopPropagation();
        event.preventDefault();
        break;
      default:
        break;
    }
  }

  toggleSelection(opt: T): void {
    this.optionSelection.toggle(opt, undefined, this.config().selectionCompareFn);
    this.emitSelection();
  }

  removeSelectedOption(opt: T) {
    this.optionSelection.toggle(opt, undefined, this.config().selectionCompareFn);
    this.emitSelection();
    this.resetField();
  }

  private syncSelection() {
    this.selectedOptions.set(this.optionSelection.selected());
  }

  private emitSelection() {
    this.syncSelection();
    const selected = this.selectedOptions();
    this.selectedData.emit(selected);
    this.selectedItem.emit(selected[0]);
  }

  clearSelections(untoggleAll: boolean = false) {
    if (!untoggleAll && this.config().savedData.length > 0) {
      this.optionSelection = new SelectionModel<T>(true, this.config().savedData);
    } else {
      this.optionSelection.selected().forEach(item => this.optionSelection.toggle(item, false));
    }

    this.emitSelection();
    this.resetField();
  }

  handleOptionClick(opt: T) {
    if (this.disabled()) return;
    if (!this.config().multiple && this.selectedOptions().length > 0) {
      return;
    }

    this.toggleSelection(opt);

    this.resetField();
    this.onInputFocus();
  }

  addNewItem(title: string) {
    if (this.config().addTransformFn == undefined || !this.config().addIfNonExisting) {
      return;
    }
    const newItem = this.config().addTransformFn(title);
    this.newItemAdded.emit(newItem);
    this.toggleSelection(newItem);

    this.resetField();
    this.onInputFocus();
  }

  /**
   *
   * @param item
   * @returns True if the item is NOT selected already
   */
  filterSelected(item: T) {
    if (this.config().unique && this.config().multiple) {
      return !this.optionSelection.isSelected(item, this.config().selectionCompareFn);
    }

    return true;
  }

  openDropdown() {
    this.hasFocus.set(true);
  }

  onInputFocus(event?: Event) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.disabled()) return;

    if (!this.config().multiple && this.selectedOptions().length > 0) {
      return;
    }

    const inputElem = this.inputElem();
    if (inputElem) {
      if (this.document.activeElement !== inputElem.nativeElement) {
        // hack: To prevent multiple typeaheads from being open at once, click document then trigger the focus
        this.document.body.click();
        inputElem.nativeElement.focus();
      }

      this.hasFocus.set(true);
      if (this.useOverlay()) {
        this.triggerWidth.set(Math.max(
          this.triggerEl().nativeElement.getBoundingClientRect().width,
          this.config().overlayMinWidth ?? 0
        ));
      }
    }


    this.openDropdown();
  }


  resetField() {
    this.formGroup.typeahead().value.set('')
    this.focusedIndex.set(0);
  }

  updateShowAddItem(options: T[]) {
    // ?! BUG This will still technically allow you to add the same thing as a previously added item. (Code will just toggle it though)
    this.showAddItem.set(false);
    if (!this.config().addIfNonExisting) return;

    const inputText = this.formModel().typeahead.trim();
    if (inputText.length < Math.max(this.config().minCharacters, 1)) return;
    if (!this.formGroup().dirty()) return; // Do we need this?

    // Check if this new option will interfere with any existing ones not shown

    if (typeof this.config().compareFnForAdd == 'function') {
      const willDuplicateExist = this.config().compareFnForAdd(this.selectedOptions(), inputText);
      if (willDuplicateExist.length > 0) {
        return;
      }
    }

    if (typeof this.config().compareFn == 'function') {
      // The problem here is that compareFn can report that duplicate will exist as it does contains not match
      const matches = this.config().compareFn(options, inputText);
      if (matches.length > 0 && matches.includes(this.config().addTransformFn(inputText))) {
        return;
      }
    }

    this.showAddItem.set(true);
    this.hasFocus.set(true);
  }

  toggleLock() {
    if (this.disabled()) return;
    this.locked.update(x => !x);

    if (!this.locked()) {
      this.onUnlock.emit();
    }
  }

}
