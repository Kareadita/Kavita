import { Component, ContentChild, ElementRef, EventEmitter, HostListener, Input, OnInit, Output, Renderer2, RendererStyleFlags2, TemplateRef, ViewChild } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable } from 'rxjs';
import { KEY_CODES } from '../shared/_services/utility.service';
import { SearchResultGroup } from '../_models/search/search-result-group';

const ITEM_QUERY_SELECTOR = '.list-group-item:not(.section-header)';

@Component({
  selector: 'app-grouped-typeahead',
  templateUrl: './grouped-typeahead.component.html',
  styleUrls: ['./grouped-typeahead.component.scss']
})
export class GroupedTypeaheadComponent implements OnInit {
  /**
   * Unique id to tie with a label element
   */
  @Input() id: string = 'grouped-typeahead';
  /**
   * Minimum number of characters in input to trigger a search
   */
  @Input() minQueryLength: number = 0;
  /**
   * Initial value of the search model
   */
  @Input() initialValue: string = '';
  @Input() grouppedData: SearchResultGroup = new SearchResultGroup();
  /**
   * Emits when the input changes from user interaction
   */
  @Output() inputChanged: EventEmitter<string> = new EventEmitter();
  /**
   * Emits when something is clicked/selected
   */
  @Output() selected: EventEmitter<any> = new EventEmitter();

  @ViewChild('input') inputElem!: ElementRef<HTMLInputElement>;
  @ContentChild('itemTemplate') itemTemplate!: TemplateRef<any>;
  @ContentChild('seriesTemplate') seriesTemplate: TemplateRef<any> | undefined;
  @ContentChild('collectionTemplate') collectionTemplate: TemplateRef<any> | undefined;
  @ContentChild('tagTemplate') tagTemplate: TemplateRef<any> | undefined;
  @ContentChild('personTemplate') personTemplate: TemplateRef<any> | undefined;
  @ContentChild('notFoundTemplate') notFoundTemplate!: TemplateRef<any>;

  hasFocus: boolean = false;
  isLoading: boolean = false;
  typeaheadForm: FormGroup = new FormGroup({});
  focusedIndex: number = 0;
  focusedIndexGroup: {[key:string]: number} = {'series': 0, 'collections': 0, 'tags': 0, 'genres': 0, 'persons': 0};


  constructor(private renderer2: Renderer2) { }

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
        // TODO: Figure out the group we are in to update focus index
        this.focusedIndex = Math.min(this.focusedIndex + 1, document.querySelectorAll(ITEM_QUERY_SELECTOR).length - 1);
        this.updateHighlight();
        break;
      }
      case KEY_CODES.UP_ARROW:
      case KEY_CODES.LEFT_ARROW:
      {
        this.focusedIndex = Math.max(this.focusedIndex - 1, 0);
        this.updateHighlight();
        // BUG: Pressing down doesn't scroll the list
        break;
      }
      case KEY_CODES.ENTER:
      {
        document.querySelectorAll(ITEM_QUERY_SELECTOR).forEach((item, index) => {
          if (item.classList.contains('active')) {
            this.handleResultlick(item);
            this.resetField();
          }
        });
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

  ngOnInit(): void {
    this.typeaheadForm.addControl('typeahead', new FormControl(this.initialValue, []));

    this.typeaheadForm.valueChanges.subscribe(change => {
      const value = this.typeaheadForm.get('typeahead')?.value;
      if (value != undefined && value.length >= this.minQueryLength) {
        this.inputChanged.emit(value);
      }
    });
  }

  onInputFocus(event: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    if (this.inputElem) {
      // hack: To prevent multiple typeaheads from being open at once, click document then trigger the focus
      document.querySelector('body')?.click();
      this.inputElem.nativeElement.focus();
      this.hasFocus = true;
    }
   
    this.openDropdown();
  }

  openDropdown() {
    setTimeout(() => {
      const model = this.typeaheadForm.get('typeahead');
      if (model) {
        model.setValue(model.value);
      }
    });
  }

  handleResultlick(item: any) {
    this.selected.emit(item);
  }

  resetField() {
    this.typeaheadForm.get('typeahead')?.setValue(this.initialValue);
    this.focusedIndex = 0;
    this.focusedIndexGroup = {'series': 0, 'collections': 0, 'tags': 0, 'genres': 0, 'persons': 0};
  }

  
  // Updates the highlight to focus on the selected item
  updateHighlight() {
    document.querySelectorAll(ITEM_QUERY_SELECTOR).forEach((item, index) => {
      if (index === this.focusedIndex && !item.classList.contains('no-hover')) {
        // apply active class
        this.renderer2.addClass(item, 'active');
      } else {
        // remove active class
        this.renderer2.removeClass(item, 'active');
      }
    });
  }

}
