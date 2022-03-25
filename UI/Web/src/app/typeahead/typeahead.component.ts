import { DOCUMENT } from '@angular/common';
import { Component, ContentChild, ElementRef, EventEmitter, HostListener, Inject, Input, OnDestroy, OnInit, Output, Renderer2, RendererStyleFlags2, TemplateRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, ReplaySubject, Subject } from 'rxjs';
import { debounceTime, filter, map, shareReplay, switchMap, take, takeUntil, tap } from 'rxjs/operators';
import { KEY_CODES } from '../shared/_services/utility.service';
import { SelectionCompareFn, TypeaheadSettings } from './typeahead-settings';

/**
   * SelectionModel<T> is used for keeping track of multiple selections. Simple interface with ability to toggle. 
   * @param selectedState Optional state to set selectedOptions to. If not passed, defaults to false.
   * @param selectedOptions Optional data elements to inform the SelectionModel of. If not passed, as toggle() occur, items are tracked.
   * @param propAccessor Optional string that points to a unique field within the T type. Used for quickly looking up.
   */
export class SelectionModel<T> {
  _data!: Array<{value: T, selected: boolean}>;
  _propAccessor: string = '';

  constructor(selectedState: boolean = false, selectedOptions: Array<T> = [], propAccessor: string = '') {
    this._data = [];

    if (propAccessor != undefined || propAccessor !== '') {
      this._propAccessor = propAccessor;
    }

    selectedOptions.forEach(d => {
      this._data.push({value: d, selected: selectedState});
    });
  }

  /**
   * Will toggle if the data item is selected or not. If data option is not tracked, will add it and set state to true.
   * @param data Item to toggle
   * @param selectedState Force the state
   * @param compareFn An optional function to use for the lookup, else will use shallowEqual implementation
   */
  toggle(data: T, selectedState?: boolean, compareFn?: SelectionCompareFn<T>) {
    let lookupMethod = this.shallowEqual;
    if (compareFn != undefined || compareFn != null) {
      lookupMethod = compareFn;
    }
    
    const dataItem = this._data.filter(d => lookupMethod(d.value, data));
    if (dataItem.length > 0) {
      if (selectedState != undefined) {
        dataItem[0].selected = selectedState;
      } else {
        dataItem[0].selected = !dataItem[0].selected;
      }
    } else {
      this._data.push({value: data, selected: (selectedState === undefined ? true : selectedState)});
    }
  }


  /**
   * Is the passed item selected
   * @param data item to check against
   * @param compareFn optional method to use to perform comparisons
   * @returns boolean
   */
  isSelected(data: T, compareFn?: SelectionCompareFn<T>): boolean {
    let dataItem: Array<any>;

    let lookupMethod = this.shallowEqual;
    if (compareFn != undefined || compareFn != null) {
      lookupMethod = compareFn;
    }
    
    dataItem = this._data.filter(d => lookupMethod(d.value, data));
    
    if (dataItem.length > 0) {
      return dataItem[0].selected;
    }
    return false;
  }

  /**
   * 
   * @returns If some of the items are selected, but not all
   */
  hasSomeSelected(): boolean {
    const selectedCount = this._data.filter(d => d.selected).length;
    return (selectedCount !== this._data.length && selectedCount !== 0)
  }

  /**
   * 
   * @returns All Selected items
   */
  selected(): Array<T> {
    return this._data.filter(d => d.selected).map(d => d.value);
  }

  /**
   * 
   * @returns All Non-Selected items
   */
   unselected(): Array<T> {
    return this._data.filter(d => !d.selected).map(d => d.value);
  }

  /**
   * 
   * @returns Last element added/tracked or undefined if nothing is tracked
   */
  peek(): T | undefined {
    if (this._data.length > 0) {
      return this._data[this._data.length - 1].value;
    }

    return undefined;
  }

  shallowEqual(object1: T, object2: T) {
    const keys1 = Object.keys(object1);
    const keys2 = Object.keys(object2);
  
    if (keys1.length !== keys2.length) {
      return false;
    }
  
    for (let key of keys1) {
      if ((object1 as any)[key] !== (object2 as any)[key]) {
        return false;
      }
    }
  
    return true;
  }
}

@Component({
  selector: 'app-typeahead',
  templateUrl: './typeahead.component.html',
  styleUrls: ['./typeahead.component.scss']
})
export class TypeaheadComponent implements OnInit, OnDestroy {
  /**
   * Settings for the typeahead
   */
  @Input() settings!: TypeaheadSettings<any>;
  /**
   * When true, component will re-init and set back to false.
   */
  @Input() reset: Subject<boolean> = new ReplaySubject(1);
  /**
   * When a field is locked, we render custom css to indicate to the user. Does not affect functionality.
   */
  @Input() locked: boolean = false;
  @Output() selectedData = new EventEmitter<any[] | any>();
  @Output() newItemAdded = new EventEmitter<any[] | any>();
  @Output() onUnlock = new EventEmitter<void>();
  @Output() lockedChange = new EventEmitter<boolean>();

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
  
  private readonly onDestroy = new Subject<void>();

  constructor(private renderer2: Renderer2, @Inject(DOCUMENT) private document: Document) { }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit() {

    this.reset.pipe(takeUntil(this.onDestroy)).subscribe((reset: boolean) => {
      this.init();
    });

    this.init();
  }

  init() {
    if (this.settings.compareFn === undefined && this.settings.multiple) {
      console.error('A compare function must be defined');
      return;
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
        tap(val => {
          if (this.inputElem != null && this.inputElem.nativeElement != null) {
            this.renderer2.setStyle(this.inputElem.nativeElement, 'width', 15 * ((this.typeaheadControl.value + '').length + 1) + 'px');
            this.focusedIndex = 0;
          }
        }),
        debounceTime(this.settings.debounce),
        filter(val => {
          // If minimum filter characters not met, do not filter
          if (this.settings.minCharacters === 0) return true;

          if (!val || val.trim().length < this.settings.minCharacters) {
            return false;
          }

          return true;
        }),
        switchMap(val => {
          this.isLoadingOptions = true;
          let results: Observable<any[]>;
          if (Array.isArray(this.settings.fetchFn)) {
            const filteredArray = this.settings.compareFn(this.settings.fetchFn, val.trim());
            results = of(filteredArray).pipe(takeUntil(this.onDestroy), map((items: any[]) => items.filter(item => this.filterSelected(item))));
          } else {
            results = this.settings.fetchFn(val.trim()).pipe(takeUntil(this.onDestroy), map((items: any[]) => items.filter(item => this.filterSelected(item))));
          }

          return results;
        }),
        tap((val) => {
          this.isLoadingOptions = false; 
          this.focusedIndex = 0; 
          this.updateShowAddItem(val);
          // setTimeout(() => {
          //   this.updateShowAddItem(val);
          //   this.updateHighlight();
          // }, 10);
          setTimeout(() => this.updateHighlight(), 20);
        }),
        shareReplay(),
        takeUntil(this.onDestroy)
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
        
        
        //this.typeaheadControl.setValue(this.settings.displayFn(this.settings.savedData))
      }
    } else {
      this.optionSelection = new SelectionModel<any>();
    }
  }


  @HostListener('window:click', ['$event'])
  handleDocumentClick(event: any) {
    this.hasFocus = false;
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) { 
    if (!this.hasFocus) { return; }

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
            this.filteredOptions.pipe(take(1)).subscribe((res: any[]) => {  
              // This isn't giving back the filtered array, but everything
              
              console.log(item.classList.contains('add-item'));
              if (this.settings.addIfNonExisting && item.classList.contains('add-item')) {
                this.addNewItem(this.typeaheadControl.value);
                this.focusedIndex = 0;
                return;
              }

              const result = this.settings.compareFn(res, (this.typeaheadControl.value || '').trim());
              if (result.length === 1) {
                this.toggleSelection(result[0]);
                this.resetField();
                this.focusedIndex = 0;
              }
            });
          }
        });
        break;
      }
      case KEY_CODES.BACKSPACE:
      case KEY_CODES.DELETE:
      {
        if (this.typeaheadControl.value !== null && this.typeaheadControl.value !== undefined && this.typeaheadControl.value.trim() !== '') {
          return;
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

  clearSelections(event: any) {
    this.optionSelection.selected().forEach(item => this.optionSelection.toggle(item, false));
    this.selectedData.emit(this.optionSelection.selected());
    this.resetField();
  }

  handleOptionClick(opt: any) {
    if (!this.settings.multiple && this.optionSelection.selected().length > 0) {
      return;
    }

    this.toggleSelection(opt);

    this.resetField();
    this.onInputFocus(undefined);
  }

  addNewItem(title: string) {
    if (this.settings.addTransformFn == undefined) {
      return;
    }
    const newItem = this.settings.addTransformFn(title);
    this.newItemAdded.emit(newItem);
    this.toggleSelection(newItem);

    this.resetField();
    this.onInputFocus(undefined);
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
    });
  }

  onInputFocus(event: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

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
    this.showAddItem = this.settings.addIfNonExisting && this.typeaheadControl.value.trim() 
          && this.typeaheadControl.value.trim().length >= Math.max(this.settings.minCharacters, 1) 
          && this.typeaheadControl.dirty
          && (typeof this.settings.compareFn == 'function' && this.settings.compareFn(options, this.typeaheadControl.value.trim()).length === 0);
    console.log('show Add item: ', this.showAddItem);
    console.log('compare func: ', this.settings.compareFn(options, this.typeaheadControl.value.trim()));

  }

  unlock(event: any) {
    this.locked = !this.locked;
    this.onUnlock.emit();
    this.lockedChange.emit(this.locked);
  }

}
