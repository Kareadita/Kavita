import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  contentChild,
  DestroyRef,
  ElementRef,
  HostListener,
  inject,
  Input,
  OnInit,
  output,
  TemplateRef,
  viewChild
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {debounceTime, distinctUntilChanged} from 'rxjs/operators';
import {KEY_CODES} from 'src/app/shared/_services/utility.service';
import {SearchResultGroup} from 'src/app/_models/search/search-result-group';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {NgClass, NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {map, startWith, tap} from "rxjs";
import {AccountService} from "../../../_services/account.service";
import {KeyBindEvent, KeyBindService} from "../../../_services/key-bind.service";
import {KeyBindTarget} from "../../../_models/preferences/preferences";
import {KeyBindPipe} from "../../../_pipes/key-bind.pipe";

export interface SearchEvent {
  value: string;
  includeFiles: boolean;
}

@Component({
  selector: 'app-grouped-typeahead',
  templateUrl: './grouped-typeahead.component.html',
  styleUrls: ['./grouped-typeahead.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NgClass, NgTemplateOutlet, TranslocoDirective, KeyBindPipe]
})
export class GroupedTypeaheadComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  protected readonly keyBindService = inject(KeyBindService);

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
  @Input() groupedData: SearchResultGroup = new SearchResultGroup();
  /**
   * Placeholder for the input
   */
  @Input() placeholder: string = '';
  /**
   * When the search is active
   */
  @Input() isLoading: boolean = false;
  /**
   * Number of milliseconds after typing before triggering inputChanged for data fetching
   */
  @Input() debounceTime: number = 200;
  /**
   * Emits when the input changes from user interaction
   */
  readonly inputChanged = output<SearchEvent>();
  /**
   * Emits when something is clicked/selected
   */
  readonly selected = output<any>();
  /**
   * Emits an event when the field is cleared
   */
  readonly clearField = output<void>();
  /**
   * Emits when a change in the search field looses/gains focus
   */
  readonly focusChanged = output<boolean>();

  readonly inputElem = viewChild.required<ElementRef<HTMLInputElement>>('input');
  readonly itemTemplate = contentChild.required<TemplateRef<any>>('itemTemplate');
  readonly seriesTemplate = contentChild<TemplateRef<any>>('seriesTemplate');
  readonly collectionTemplate = contentChild<TemplateRef<any>>('collectionTemplate');
  readonly tagTemplate = contentChild<TemplateRef<any>>('tagTemplate');
  readonly personTemplate = contentChild<TemplateRef<any>>('personTemplate');
  readonly genreTemplate = contentChild<TemplateRef<any>>('genreTemplate');
  readonly noResultsTemplate = contentChild<TemplateRef<any>>('noResultsTemplate');
  readonly libraryTemplate = contentChild<TemplateRef<any>>('libraryTemplate');
  readonly readingListTemplate = contentChild<TemplateRef<any>>('readingListTemplate');
  readonly fileTemplate = contentChild<TemplateRef<any>>('fileTemplate');
  readonly chapterTemplate = contentChild<TemplateRef<any>>('chapterTemplate');
  readonly bookmarkTemplate = contentChild<TemplateRef<any>>('bookmarkTemplate');
  readonly annotationTemplate = contentChild<TemplateRef<any>>('annotationTemplate');
  readonly extraTemplate = contentChild<TemplateRef<any>>('extraTemplate');


  hasFocus: boolean = false;
  typeaheadForm: FormGroup = new FormGroup({
    typeahead: new FormControl('', []),
  });
  includeChapterAndFiles: boolean = false;
  prevSearchTerm: string = '';
  searchSettingsForm = new FormGroup(({'includeExtras': new FormControl(false)}));

  get searchTerm() {
    return this.typeaheadForm.get('typeahead')?.value || '';
  }

  get hasData() {
    return !(this.noResultsTemplate != undefined && !this.groupedData.persons.length && !this.groupedData.collections.length
      && !this.groupedData.series.length && !this.groupedData.persons.length && !this.groupedData.tags.length && !this.groupedData.genres.length && !this.groupedData.libraries.length
      && !this.groupedData.files.length && !this.groupedData.chapters.length && !this.groupedData.bookmarks.length);
  }


  @HostListener('window:click', ['$event'])
  handleDocumentClick(event: MouseEvent) {
    this.close();
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    switch(event.key) {
      case KEY_CODES.ESC_KEY:
        if (!this.hasFocus) { return; }
        this.close();
        event.stopPropagation();
        break;
      default:
        break;
    }
  }

  private focusElement(e: KeyBindEvent) {
    const inputElem = this.inputElem();
    if (inputElem.nativeElement) {
      e.triggered = true;
      inputElem.nativeElement.focus();
      inputElem.nativeElement.click();
    }
  }

  ngOnInit(): void {
    this.typeaheadForm.get('typeahead')?.setValue(this.initialValue);
    this.cdRef.markForCheck();

    this.keyBindService.registerListener(
      this.destroyRef,
      (e) => this.focusElement(e),
      [KeyBindTarget.OpenSearch],
      {fireInEditable: true},
    );

    this.searchSettingsForm.get('includeExtras')!.valueChanges.pipe(
      startWith(false),
      map(val => {
        if (val === null) return false;
        return val;
      }),
      distinctUntilChanged(),
      tap((val: boolean) => this.toggleIncludeFiles(val)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    this.typeaheadForm.valueChanges.pipe(
      debounceTime(this.debounceTime),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(change => {
      const value = this.typeaheadForm.get('typeahead')?.value;

      if (value != undefined && value != '' && !this.hasFocus) {
        this.hasFocus = true;
        this.cdRef.markForCheck();
      }

      if (value != undefined && value.length >= this.minQueryLength) {

        if (this.prevSearchTerm === value) return;
        this.inputChanged.emit({value, includeFiles: this.includeChapterAndFiles});
        this.prevSearchTerm = value;
        this.cdRef.markForCheck();
      }
    });
  }

  onInputFocus(event: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    this.openDropdown();
    return this.hasFocus;
  }

  openDropdown() {
    setTimeout(() => {
      const model = this.typeaheadForm.get('typeahead');
      if (model) {
        model.setValue(model.value);
      }
    });
  }

  handleResultClick(item: any) {
    this.selected.emit(item);
  }

  toggleIncludeFiles(val: boolean) {
    const firstRun = !val && val === this.includeChapterAndFiles;

    this.includeChapterAndFiles = val;
    this.inputChanged.emit({value: this.searchTerm, includeFiles: this.includeChapterAndFiles});

    if (!firstRun) {
      this.hasFocus = true;
      const inputElem = this.inputElem();
      if (inputElem && inputElem.nativeElement) {
        inputElem.nativeElement.focus();
      }

      this.openDropdown();
    }


    this.cdRef.markForCheck();
  }

  resetField() {
    this.prevSearchTerm = '';
    this.typeaheadForm.get('typeahead')?.setValue(this.initialValue);
    this.clearField.emit(undefined);
    this.cdRef.markForCheck();
  }


  close(event?: FocusEvent) {
    if (event) {
      // If the user is tabbing out of the input field, check if there are results first before closing
      if (this.hasData || this.searchTerm) {
        return;
      }
    }
    if (this.searchTerm === '') {
      this.resetField();
    }
    this.hasFocus = false;
    this.cdRef.markForCheck();
    this.focusChanged.emit(this.hasFocus);
  }

  open(event?: FocusEvent) {
    this.hasFocus = true;
    this.focusChanged.emit(this.hasFocus);
    this.cdRef.markForCheck();
  }

  public clear() {
    this.prevSearchTerm = '';
    this.typeaheadForm.get('typeahead')?.setValue(this.initialValue);
    this.cdRef.markForCheck();
  }

}
