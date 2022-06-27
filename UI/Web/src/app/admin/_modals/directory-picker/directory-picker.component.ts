import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { NgbActiveModal, NgbTypeahead } from '@ng-bootstrap/ng-bootstrap';
import { catchError, debounceTime, distinctUntilChanged, filter, map, merge, Observable, of, OperatorFunction, Subject, switchMap, tap } from 'rxjs';
import { Stack } from 'src/app/shared/data-structures/stack';
import { DirectoryDto } from 'src/app/_models/system/directory-dto';
import { LibraryService } from '../../../_services/library.service';


export interface DirectoryPickerResult {
  success: boolean;
  folderPath: string;
}



@Component({
  selector: 'app-directory-picker',
  templateUrl: './directory-picker.component.html',
  styleUrls: ['./directory-picker.component.scss']
})
export class DirectoryPickerComponent implements OnInit {

  @Input() startingFolder: string = '';
  /**
   * Url to give more information about selecting directories. Passing nothing will suppress. 
   */
  @Input() helpUrl: string = 'https://wiki.kavitareader.com/en/guides/first-time-setup#adding-a-library-to-kavita';

  currentRoot = '';
  folders: DirectoryDto[] = [];
  routeStack: Stack<string> = new Stack<string>();


  path: string = '';
  @ViewChild('instance', {static: true}) instance!: NgbTypeahead;
  focus$ = new Subject<string>();
  click$ = new Subject<string>();
  searching: boolean = false;
  searchFailed: boolean = false;


  search: OperatorFunction<string, readonly string[]> = (text$: Observable<string>) => {
    const debouncedText$ = text$.pipe(debounceTime(200), distinctUntilChanged());
    const clicksWithClosedPopup$ = this.click$.pipe(filter(() => !this.instance.isPopupOpen()));
    const inputFocus$ = this.focus$;

    return merge(debouncedText$, inputFocus$, clicksWithClosedPopup$, text$).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.searching = true),
      switchMap(term =>
        this.libraryService.listDirectories(this.path).pipe(
          tap(() => this.searchFailed = false),
          tap((folders) => this.folders = folders),
          map(folders => folders.map(f => f.fullPath)),
          catchError(() => {
            this.searchFailed = true;
            return of([]);
          }))
      ),
      tap(() => this.searching = false)
    )
  }

  constructor(public modal: NgbActiveModal, private libraryService: LibraryService) {

  }

  ngOnInit(): void {
    if (this.startingFolder && this.startingFolder.length > 0) {
      let folders = this.startingFolder.split('/');
      let folders2 = this.startingFolder.split('\\');
      if (folders.length === 1 && folders2.length > 1) {
        folders = folders2;
      }
      if (!folders[0].endsWith('/')) {
        folders[0] = folders[0] + '/';
      }
      folders.forEach(folder => this.routeStack.push(folder));

      const fullPath = this.routeStack.items.join('/');
      this.loadChildren(fullPath);
    } else {
      this.loadChildren(this.currentRoot);
    }
  }

  updateTable() {
    this.loadChildren(this.path);
  }


  selectNode(folder: DirectoryDto) {
    if (folder.disabled) return;
    this.currentRoot = folder.name;
    this.routeStack.push(folder.name);
    this.path = folder.fullPath;
    this.loadChildren(this.path);
  }

  goBack() {
    this.routeStack.pop();
    const stackPeek = this.routeStack.peek();
    if (stackPeek !== undefined) {
      this.currentRoot = stackPeek;
      const fullPath = this.routeStack.items.join('/');
      this.loadChildren(fullPath);
    } else {
      this.currentRoot = '';
      this.loadChildren(this.currentRoot);
    }
  }

  loadChildren(path: string) {
    this.libraryService.listDirectories(path).subscribe(folders => {
      this.folders = folders;
    }, err => {
      // If there was an error, pop off last directory added to stack
      this.routeStack.pop();
      const item = this.folders.find(f => f.fullPath === path);
      if (item) {
        item.disabled = true;
      }
    });
  }

  shareFolder(fullPath: string, event: any) {
    event.preventDefault();
    event.stopPropagation();

    this.modal.close({success: true, folderPath: fullPath});
  }

  share() {
    this.modal.close({success: true, folderPath: this.path});
  }

  close() {
    this.modal.close({success: false, folderPath: undefined});
  }

  getStem(path: string): string {

    const lastPath = this.routeStack.peek();
    if (lastPath && lastPath != path) {
      let replaced = path.replace(lastPath, '');
      if (replaced.startsWith('/') || replaced.startsWith('\\')) {
        replaced = replaced.substring(1, replaced.length);
      }
      return replaced;
    }

    return path;
  }

  navigateTo(index: number) {
    while(this.routeStack.items.length - 1 > index) {
      this.routeStack.pop();
    }
    
    const fullPath = this.routeStack.items.join('/');
    this.path = fullPath;
    this.loadChildren(fullPath);
  }
}


