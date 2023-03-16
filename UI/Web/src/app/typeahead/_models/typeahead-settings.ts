import { Observable } from 'rxjs';
import { FormControl } from '@angular/forms';

export type SelectionCompareFn<T> = (a: T, b: T) => boolean;

export class TypeaheadSettings<T> {
    /**
     * How many ms between typing actions before pipeline to load data is triggered
     */
    debounce: number = 200;
    /**
     * Multiple options can be selected from dropdown. Will be rendered as tag badges.
     */
    multiple: boolean = false;
    /**
     * Id of the input element, for linking label elements (accessibility)
     */
    id: string = '';
    /**
     * Show a locked icon next to input and provide functionality around locking/unlocking a field
     */
    showLocked: boolean = false;
    /**
     * Data to preload the typeahead with on first load
     */
    /**
     * Data to preload the typeahead with on first load
     */
    savedData!: T[] | T;
    /**
     * Function to compare the elements. Should return all elements that fit the matching criteria. 
     * This is only used with non-Observable based fetchFn, but must be defined for all uses of typeahead.
     */
    compareFn!: ((optionList: T[], filter: string)  => T[]);
    /**
     * Must be defined when addIfNonExisting is true. Used to ensure no duplicates exist when adding.
     */
    compareFnForAdd!: ((optionList: T[], filter: string)  => T[]);
    /**
     * Function which is used for comparing objects when keeping track of state. 
     * Useful over shallow equal when you have image urls that have random numbers on them.
     */ 
    selectionCompareFn?: SelectionCompareFn<T>;
    /**
     * Function to fetch the data from the server. If data is mainatined in memory, wrap in an observable.
     */
    fetchFn!: (filter: string) => Observable<T[]>;
    /**
     * Minimum number of characters needed to type to trigger the fetch pipeline
     */
    minCharacters: number = 1;
    /**
     * Optional form Control to tie model to. 
     */
    formControl?: FormControl;
    /**
     * If true, typeahead will remove already selected items from fetchFn results. Only appies when multiple=true
     */
    unique: boolean = true;
    /**
     * If true, will fire an event for newItemAdded and will prompt the user to add form model to the list of selected items
     */
    addIfNonExisting: boolean = false;
    /**
     * Required for addIfNonExisting to transform the text from model into the item
     */
    addTransformFn!: (text: string) => T;
    /**
     * An optional, but recommended trackby identity function to help Angular render the list better
     */
    trackByIdentityFn!: (index: number, value: T) => T;
}