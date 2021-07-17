import { Component, ContentChild, ElementRef, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output, Renderer2, RendererStyleFlags2, TemplateRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, Observer, of, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, last, map, shareReplay, switchMap, take, takeLast, takeUntil, tap, withLatestFrom } from 'rxjs/operators';
import { KEY_CODES } from '../shared/_services/utility.service';
import { TypeaheadSettings } from './typeahead-settings';

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

  // __lookupItem(item: T) {
  //   if (this._propAccessor != '') {
  //     // TODO: Implement this code to speedup lookups (use a map rather than array)
  //   }
  //   const dataItem = this._data.filter(data => data.value == d);
  // }

  /**
   * Will toggle if the data item is selected or not. If data option is not tracked, will add it and set state to true.
   * @param data Item to toggle
   */
  toggle(data: T, selectedState?: boolean) {
    //const dataItem = this._data.filter(d => d.value == data);
    const dataItem = this._data.filter(d => this.shallowEqual(d.value, data));
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
  isSelected(data: T, compareFn?: ((d: T) => boolean)): boolean {
    let dataItem: Array<any>;
    if (compareFn != undefined || compareFn != null) {
      dataItem = this._data.filter(d => compareFn(d.value));
    } else {
      dataItem = this._data.filter(d => this.shallowEqual(d.value, data));
    }
    
    if (dataItem.length > 0) {
      return dataItem[0].selected;
    }
    return false;
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

  filteredOptions!: Observable<string[]>;
  isLoadingOptions: boolean = false;
  typeaheadControl!: FormControl;
  typeaheadForm!: FormGroup;
  

  @Input() settings!: TypeaheadSettings<any>;
  @Output() selectedData = new EventEmitter<any[] | any>();
  @Output() newItemAdded = new EventEmitter<any[] | any>();

  optionSelection!: SelectionModel<any>;

  hasFocus = false; // Whether input has active focus
  focusedIndex: number = 0;
  showAddItem: boolean = false;

  @ViewChild('input') inputElem!: ElementRef<HTMLInputElement>;
  @ContentChild('optionItem') optionTemplate!: TemplateRef<any>;
  @ContentChild('badgeItem') badgeTemplate!: TemplateRef<any>;
  
  private readonly onDestroy = new Subject<void>();

  constructor(private renderer2: Renderer2) { }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit() {

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
            results = of(filteredArray).pipe(map((items: any[]) => items.filter(item => this.filterSelected(item))));
          } else {
            results = this.settings.fetchFn(val.trim()).pipe(map((items: any[]) => items.filter(item => this.filterSelected(item))));
          }

          return results;
        }),
        tap((val) => {
          this.isLoadingOptions = false; 
          this.focusedIndex = 0; 
          setTimeout(() => {
            this.updateShowAddItem(val);
            this.updateHighlight();
          }, 10);
          setTimeout(() => this.updateHighlight(), 20);
        }),
        shareReplay(),
        takeUntil(this.onDestroy)
      );


    if (this.settings.savedData) {
      if (this.settings.multiple) {
        this.optionSelection = new SelectionModel<any>(true, this.settings.savedData);  
      }
      //  else {
      //   this.optionSelection = new SelectionModel<any>(true, this.settings.savedData[0]);
      //   this.typeaheadControl.setValue(this.settings.displayFn(this.settings.savedData))
      // }
    } else {
      this.optionSelection = new SelectionModel<any>();
    }
  }


  @HostListener('window:click', ['$event'])
  handleDocumentClick() {
    this.hasFocus = false;
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) { 
    if (!this.hasFocus) { return; }

    switch(event.key) {
      case KEY_CODES.DOWN_ARROW:
      case KEY_CODES.RIGHT_ARROW:
      {
        this.focusedIndex = Math.min(this.focusedIndex + 1, document.querySelectorAll('.list-group-item').length - 1);
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
        document.querySelectorAll('.list-group-item').forEach((item, index) => {
          if (item.classList.contains('active')) {
            this.filteredOptions.pipe(take(1)).subscribe((res: any[]) => {  
              // This isn't giving back the filtered array, but everything
              const result = this.settings.compareFn(res, (item.textContent || '').trim());
              if (result.length === 1) {
                if (item.classList.contains('add-item')) {
                  this.addNewItem(this.typeaheadControl.value);
                } else {
                  this.toggleSelection(result[0]);
                }
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
    this.optionSelection.toggle(opt);
    this.selectedData.emit(this.optionSelection.selected());
  }

  removeSelectedOption(opt: any) {
    this.optionSelection.toggle(opt);
    this.selectedData.emit(this.optionSelection.selected());
    this.resetField();
  }

  handleOptionClick(opt: any) {
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

  filterSelected(item: any) {
    if (this.settings.unique && this.settings.multiple) {
      return !this.optionSelection.isSelected(item);
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

    if (this.inputElem) {
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
    document.querySelectorAll('.list-group-item').forEach((item, index) => {
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

  }

}
