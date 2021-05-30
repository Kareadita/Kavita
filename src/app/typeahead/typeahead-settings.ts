import { Observable } from 'rxjs';
import { FormControl } from '@angular/forms';

export class TypeaheadSettings {
    debounce: number = 200;
    multiple: boolean = false;
    id: string = '';
    savedData: any[] | any;
    compareFn!:  ((optionList: any[], filter: string)  => any[]);// = undefined; 
    fetchFn!: ((filter: string) => Observable<any[]>) | any[] ; // | []
    displayFn!: ( (data: any) => string);
    minCharacters: number = 1;
    formControl?: FormControl;
    unique: boolean = true; // If true, typeahead will remove already selected items from fetchFn results. Only applies with multiple=true
    
    addIfNonExisting: boolean = false; // If true, add newItemAdded event handler
    addTransformFn!: (text: string) => any;

}