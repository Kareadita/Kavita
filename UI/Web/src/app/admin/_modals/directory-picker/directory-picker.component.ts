import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Stack } from 'src/app/shared/data-structures/stack';
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
  @Input() helpUrl: string = 'https://wiki.kavitareader.com/en/guides/adding-a-library';

  currentRoot = '';
  folders: string[] = [];
  routeStack: Stack<string> = new Stack<string>();
  filterQuery: string = '';

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

  filterFolder = (folder: string) => {
    return folder.toLowerCase().indexOf((this.filterQuery || '').toLowerCase()) >= 0;
  }

  selectNode(folderName: string) {
    this.currentRoot = folderName;
    this.routeStack.push(folderName);
    const fullPath = this.routeStack.items.join('/');
    this.loadChildren(fullPath); 
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
    });
  }

  shareFolder(folderName: string, event: any) {
    event.preventDefault();
    event.stopPropagation();

    let fullPath = folderName;
    if (this.routeStack.items.length > 0) {
      const pathJoin = this.routeStack.items.join('/');
      fullPath = pathJoin + ((pathJoin.endsWith('/') || pathJoin.endsWith('\\')) ? '' : '/') + folderName;
    }

    this.modal.close({success: true, folderPath: fullPath});
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
    this.loadChildren(fullPath);
  }
}
