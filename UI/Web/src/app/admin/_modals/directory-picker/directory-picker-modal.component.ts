import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {NgbActiveModal, NgbHighlight, NgbTypeahead} from '@ng-bootstrap/ng-bootstrap';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  map,
  merge,
  Observable,
  of,
  OperatorFunction,
  Subject,
  switchMap,
  tap
} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {LibraryService} from '../../../_services/library.service';
import {NgClass} from '@angular/common';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {DirectoryDto} from "../../../_models/system/directory-dto";
import {Stack} from "../../../shared/data-structures/stack";
import {FormFieldDirective} from "../../../_directives/form-field.directive";


export interface DirectoryPickerResult {
  success: boolean;
  folderPath: string;
}

@Component({
    selector: 'app-directory-picker-modal',
    templateUrl: './directory-picker-modal.component.html',
    styleUrls: ['./directory-picker-modal.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, NgbTypeahead, NgbHighlight, NgClass, TranslocoDirective, FormFieldDirective]
})
export class DirectoryPickerModalComponent implements OnInit {
  protected readonly modal = inject(NgbActiveModal);
  private readonly libraryService = inject(LibraryService);
  private readonly destroyRef = inject(DestroyRef);

  startingFolder = input<string>('');
  /** Url to give more information about selecting directories. Passing nothing will suppress. */
  helpUrl = input<string>(WikiLink.Library);
  protected readonly instance = viewChild<NgbTypeahead>('instance');


  currentRoot = signal('');
  folders = signal<DirectoryDto[]>([]);
  searching = signal(false);
  searchFailed = signal(false);
  routeItems = signal<string[]>([]);
  routeStackPeek = computed(() => {
    const items = this.routeItems();
    return items.length > 0 ? items[items.length - 1] : undefined;
  });

  routeStack = new Stack<string>();
  pathControl = new FormControl('', {nonNullable: true});
  focus$ = new Subject<string>();
  click$ = new Subject<string>();

  search: OperatorFunction<string, readonly string[]> = (text$: Observable<string>) => {
    const debouncedText$ = text$.pipe(debounceTime(200), distinctUntilChanged());
    const clicksWithClosedPopup$ = this.click$.pipe(filter(() => !this.instance()!.isPopupOpen()));
    const inputFocus$ = this.focus$;

    return merge(debouncedText$, inputFocus$, clicksWithClosedPopup$, text$).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.searching.set(true)),
      switchMap(term =>
        this.libraryService.listDirectories(this.pathControl.value).pipe(
          tap(() => this.searchFailed.set(false)),
          tap((folders) => this.folders.set(folders)),
          map(folders => folders.map(f => f.fullPath)),
          catchError(() => {
            this.searchFailed.set(true);
            return of([]);
          }))
      ),
      tap(() => this.searching.set(false))
    )
  }

  ngOnInit(): void {
    this.pathControl.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.updateTable());

    if (this.startingFolder() && this.startingFolder().length > 0) {
      let folders = this.startingFolder().split('/');
      let folders2 = this.startingFolder().split('\\');
      if (folders.length === 1 && folders2.length > 1) {
        folders = folders2;
      }
      if (!folders[0].endsWith('/')) {
        folders[0] = folders[0] + '/';
      }
      folders.forEach(folder => this.routeStack.push(folder));
      this.syncRouteItems();

      const fullPath = this.routeStack.items.join('/');
      this.loadChildren(fullPath);
    } else {
      this.loadChildren(this.currentRoot());
    }
  }

  updateTable() {
    this.loadChildren(this.pathControl.value);
  }

  selectNode(folder: DirectoryDto) {
    if (folder.disabled) return;
    this.currentRoot.set(folder.name);
    this.routeStack.push(folder.name);
    this.syncRouteItems();
    this.pathControl.setValue(folder.fullPath, {emitEvent: false});
    this.loadChildren(this.pathControl.value);
  }

  goBack() {
    this.routeStack.pop();
    this.syncRouteItems();
    const stackPeek = this.routeStack.peek();
    if (stackPeek !== undefined) {
      this.currentRoot.set(stackPeek);
      const fullPath = this.routeStack.items.join('/');
      this.loadChildren(fullPath);
    } else {
      this.currentRoot.set('');
      this.loadChildren(this.currentRoot());
    }
  }

  loadChildren(path: string) {
    this.libraryService.listDirectories(path).subscribe({
      next: folders => {
        this.folders.set(folders);
      },
      error: err => {
        // If there was an error, pop off last directory added to stack
        this.routeStack.pop();
        this.syncRouteItems();
        this.folders.update(current => {
          return current.map(f => f.fullPath === path ? {...f, disabled: true} : f);
        });
      }
    });
  }

  share() {
    this.modal.close({success: true, folderPath: this.pathControl.value});
  }

  close() {
    this.modal.close({success: false, folderPath: undefined});
  }

  navigateTo(index: number) {
    while(this.routeStack.items.length - 1 > index) {
      this.routeStack.pop();
    }
    this.syncRouteItems();

    const fullPath = this.routeStack.items.join('/');
    this.pathControl.setValue(fullPath, {emitEvent: false});
    this.loadChildren(fullPath);
  }

  private syncRouteItems() {
    this.routeItems.set([...this.routeStack.items]);
  }
}
