import { trigger, state, style, transition, animate } from '@angular/animations';
import {CommonModule, DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Inject,
  Input,
  OnInit,
  Output,
  Renderer2,
  RendererStyleFlags2,
  TemplateRef,
  ViewChild
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import { Observable, ReplaySubject } from 'rxjs';
import { auditTime, filter, map, shareReplay, switchMap, take, tap } from 'rxjs/operators';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from '../_models/typeahead-settings';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {SelectionModel} from "../_models/selection-model";


const ANIMATION_SPEED = 200;

@Component({
  selector: 'app-typeahead',
  standalone: true,
  imports: [CommonModule, TagBadgeComponent, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './typeahead.component.html',
  styleUrls: ['./typeahead.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [
    trigger('slideFromTop', [
      state('in', style({ height: '0px'})),
      transition('void => *', [
        style({ height: '100%', overflow: 'auto' }),
        animate(ANIMATION_SPEED)
      ]),
      transition('* => void', [
        animate(ANIMATION_SPEED, style({ height: '0px' })),
      ])
    ])
  ]
})
export class TypeaheadComponent implements OnInit {
  /**
   * Settings for the typeahead
   */
  @Input({required: true}) settings!: TypeaheadSettings<any>;
  /**
   * When true, will reset field to no selections. When false, will reset to saved data
   */
  @Input() reset: ReplaySubject<boolean> = new ReplaySubject(1);
  /**
   * When a field is locked, we render custom css to indicate to the user. Does not affect functionality.
   */
  @Input() locked: boolean = false;
  /**
   * If disabled, a user will not be able to interact with the typeahead
   */
  @Input() disabled: boolean = false;
  /**
   * When triggered, will focus the input if the passed string matches the id
   */
  @Input() focus: EventEmitter<string> | undefined;
  @Output() selectedData = new EventEmitter<any[] | any>();
  @Output() newItemAdded = new EventEmitter<any[] | any>();
  // eslint-disable-next-line @angular-eslint/no-output-on-prefix
  @Output() onUnlock = new EventEmitter<void>();
  @Output() lockedChange = new EventEmitter<boolean>();
  private readonly destroyRef = inject(DestroyRef);


  @ViewChild('input') inputElem!: ElementRef<HTMLInputElement>;
  @ContentChild('optionItem') optionTemplate!: TemplateRef<any>;
  @ContentChild('badgeItem') badgeTemplate!: TemplateRef<any>;

  optionSelection!: SelectionModel<any>;

  hasFocus = false; // Whether input has active focus
  focusedIndex: number = 0;
  showAddItem: boolean = false;
  filteredOptions!: Observable<string[]>;
  isLoadingOptions: boolean = false;
  typeaheadControl!: FormControl;
  typeaheadForm!: FormGroup;

  constructor(private renderer2: Renderer2, @Inject(DOCUMENT) private document: Document, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit() {
    this.reset.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((resetToEmpty: boolean) => {
      this.clearSelections(resetToEmpty);
      this.init();
    });

    if (this.focus) {
      this.focus.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id: string) => {
        if (this.settings.id !== id) return;
        this.onInputFocus();
      });
    }

    this.init();
  }

  init() {
    if (this.settings.compareFn === undefined && this.settings.multiple) {
      console.error('A compare function must be defined');
      return;
    }

    if (this.settings.trackByIdentityFn === undefined) {
      this.settings.trackByIdentityFn = (index, value) => value;
    }

    if (this.settings.hasOwnProperty('formControl') && this.settings.formControl) {
      this.typeaheadControl = this.settings.formControl;
    } else {
      this.typeaheadControl = new FormControl('');
    }
    this.typeaheadForm = new FormGroup({
      'typeahead': this.typeaheadControl
    });

    this.filteredOptions = this.typeaheadForm.get('typeahead')!.valueChanges
      .pipe(
        // Adjust input box to grow
        tap((val: string) => {
          if (this.inputElem != null && this.inputElem.nativeElement != null) {
            this.renderer2.setStyle(this.inputElem.nativeElement, 'width', 15 * (val.trim().length + 1) + 'px');
            this.focusedIndex = 0;
          }
        }),
        map((val: string) => val.trim()),
        auditTime(this.settings.debounce),
        //distinctUntilChanged(), // ?!: BUG Doesn't trigger the search to run when filtered array changes
        filter((val: string) => {
          // If minimum filter characters not met, do not filter
          if (this.settings.minCharacters === 0) return true;

          if (!val || val.length < this.settings.minCharacters) {
            return false;
          }

          return true;
        }),

        switchMap((val: string) => {
          this.isLoadingOptions = true;
          return this.settings.fetchFn(val.trim()).pipe(takeUntilDestroyed(this.destroyRef), map((items: any[]) => items.filter(item => this.filterSelected(item))));
        }),
        tap((filteredOptions: any[]) => {
          this.isLoadingOptions = false;
          this.focusedIndex = 0;
          this.cdRef.markForCheck();
          setTimeout(() => {
            this.updateShowAddItem(filteredOptions);
            this.updateHighlight();
          }, 10);
          setTimeout(() => this.updateHighlight(), 20);

        }),
        shareReplay(),
        takeUntilDestroyed(this.destroyRef)
      );


    if (this.settings.savedData) {
      if (this.settings.multiple) {
        this.optionSelection = new SelectionModel<any>(true, this.settings.savedData);
      }
       else {
         const isArray = this.settings.savedData.hasOwnProperty('length');
         if (isArray) {
          this.optionSelection = new SelectionModel<any>(true, this.settings.savedData);
         } else {
          this.optionSelection = new SelectionModel<any>(true, [this.settings.savedData]);
         }
      }
    } else {
      this.optionSelection = new SelectionModel<any>();
    }
  }


  @HostListener('body:click', ['$event'])
  handleDocumentClick(event: any) {
    // Don't close the typeahead when we select an item from it
    if (event.target && (event.target as HTMLElement).classList.contains('list-group-item')) {
      return;
    }
    this.hasFocus = false;
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (!this.hasFocus) { return; }
    if (this.disabled) return;

    switch(event.key) {
      case KEY_CODES.DOWN_ARROW:
      case KEY_CODES.RIGHT_ARROW:
      {
        this.focusedIndex = Math.min(this.focusedIndex + 1, this.document.querySelectorAll('.list-group-item').length - 1);
        this.updateHighlight();
        break;
      }
      case KEY_CODES.UP_ARROW:
      case KEY_CODES.LEFT_ARROW:
      {
        this.focusedIndex = Math.max(this.focusedIndex - 1, 0);
        this.updateHighlight();
        break;
      }
      case KEY_CODES.ENTER:
      {
        this.document.querySelectorAll('.list-group-item').forEach((item, index) => {
          if (item.classList.contains('active')) {
            this.filteredOptions.pipe(take(1)).subscribe((opts: any[]) => {
              // This isn't giving back the filtered array, but everything
              event.preventDefault();
              event.stopPropagation();

              (item as HTMLElement).click();
              this.focusedIndex = 0;
            });
          }
        });
        break;
      }
      case KEY_CODES.BACKSPACE:
      case KEY_CODES.DELETE:
      {
        if (this.typeaheadControl.value !== null && this.typeaheadControl.value !== undefined && this.typeaheadControl.value.trim() !== '') {
          break;
        }
        const selected = this.optionSelection.selected();
        if (selected.length > 0) {
          this.removeSelectedOption(selected.pop());
        }
        break;
      }
      case KEY_CODES.ESC_KEY:
        this.hasFocus = false;
        event.stopPropagation();
        event.preventDefault();
        break;
      default:
        break;
    }
  }

  toggleSelection(opt: any): void {
    this.optionSelection.toggle(opt, undefined, this.settings.selectionCompareFn);
    this.selectedData.emit(this.optionSelection.selected());
  }

  removeSelectedOption(opt: any) {
    this.optionSelection.toggle(opt, undefined, this.settings.selectionCompareFn);
    this.selectedData.emit(this.optionSelection.selected());
    this.resetField();
  }

  clearSelections(untoggleAll: boolean = false) {
    if (this.optionSelection) {
      if (!untoggleAll && this.settings.savedData) {
        const isArray = this.settings.savedData.hasOwnProperty('length');
         if (isArray) {
          this.optionSelection = new SelectionModel<any>(true, this.settings.savedData); // NOTE: Library-detail will break the 'x' button due to how savedData is being set to avoid state reset
         } else {
          this.optionSelection = new SelectionModel<any>(true, [this.settings.savedData]);
         }
         this.cdRef.markForCheck();
      } else {
        this.optionSelection.selected().forEach(item => this.optionSelection.toggle(item, false));
        this.cdRef.markForCheck();
      }

      this.selectedData.emit(this.optionSelection.selected());
      this.resetField();
    }
  }

  handleOptionClick(opt: any) {
    if (this.disabled) return;
    if (!this.settings.multiple && this.optionSelection.selected().length > 0) {
      return;
    }

    this.toggleSelection(opt);

    this.resetField();
    this.onInputFocus();
  }

  addNewItem(title: string) {
    if (this.settings.addTransformFn == undefined || !this.settings.addIfNonExisting) {
      return;
    }
    const newItem = this.settings.addTransformFn(title);
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
  filterSelected(item: any) {
    if (this.settings.unique && this.settings.multiple) {
      return !this.optionSelection.isSelected(item, this.settings.selectionCompareFn);
    }

    return true;
  }

  openDropdown() {
    setTimeout(() => {
      this.typeaheadControl.setValue(this.typeaheadControl.value);
      this.hasFocus = true;
    });
  }

  onInputFocus(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (this.disabled) return;

    if (!this.settings.multiple && this.optionSelection.selected().length > 0) {
      return;
    }

    if (this.inputElem) {
      // hack: To prevent multiple typeaheads from being open at once, click document then trigger the focus
      this.document.body.click();

      this.inputElem.nativeElement.focus();
      this.hasFocus = true;
    }


    this.openDropdown();
  }


  resetField() {
    if (this.inputElem && this.inputElem.nativeElement) {
      this.renderer2.setStyle(this.inputElem.nativeElement, 'width', 4, RendererStyleFlags2.Important);
    }
    this.typeaheadControl.setValue('');
    this.focusedIndex = 0;
  }

  // Updates the highlight to focus on the selected item
  updateHighlight() {
    this.document.querySelectorAll('.list-group-item').forEach((item, index) => {
      if (index === this.focusedIndex && !item.classList.contains('no-hover')) {
        // apply active class
        this.renderer2.addClass(item, 'active');
      } else {
        // remove active class
        this.renderer2.removeClass(item, 'active');
      }
    });
  }

  updateShowAddItem(options: any[]) {
    // ?! BUG This will still technicially allow you to add the same thing as a previously added item. (Code will just toggle it though)
    this.showAddItem = false;
    this.cdRef.markForCheck();
    if (!this.settings.addIfNonExisting) return;

    const inputText = this.typeaheadControl.value.trim();
    if (inputText.length < Math.max(this.settings.minCharacters, 1)) return;
    if (!this.typeaheadControl.dirty) return; // Do we need this?

    // Check if this new option will interfere with any existing ones not shown

    if (typeof this.settings.compareFnForAdd == 'function') {
      const willDuplicateExist = this.settings.compareFnForAdd(this.optionSelection.selected(), inputText);
      if (willDuplicateExist.length > 0) {
        return;
      }
    }

    if (typeof this.settings.compareFn == 'function') {
      // The problem here is that compareFn can report that duplicate will exist as it does contains not match
      const matches = this.settings.compareFn(options, inputText);
      if (matches.length > 0 && matches.includes(this.settings.addTransformFn(inputText))) {
        return;
      }
    }

    this.showAddItem = true;

    if (this.showAddItem) {
      this.hasFocus = true;
    }
    this.cdRef.markForCheck();
  }

  toggleLock(event: any) {
    if (this.disabled) return;
    this.locked = !this.locked;
    this.lockedChange.emit(this.locked);

    if (!this.locked) {
      this.onUnlock.emit();
    }
  }

}
