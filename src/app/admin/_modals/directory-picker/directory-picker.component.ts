import { Component, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { LibraryService } from '../../../_services/library.service';

class Stack {
  items: any[];

  constructor() {
    this.items = [];
  }

  isEmpty() {
    return this.items.length === 0;
  }

  peek() {
    if (!this.isEmpty()) {
      return this.items[this.items.length - 1];
    }
  }

  pop() {
    if (this.isEmpty()) {
      return undefined;
    }
    return this.items.pop();
  }

  push(item: any) {
    this.items.push(item);
  }
}

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

  currentRoot = '';
  folders: string[] = [];
  routeStack: Stack = new Stack();

  constructor(public modal: NgbActiveModal, private libraryService: LibraryService) {

  }

  ngOnInit(): void {
    this.loadChildren(this.currentRoot);
  }

  selectNode(folderName: string) {
    this.currentRoot = folderName;
    this.routeStack.push(folderName);
    const fullPath = this.routeStack.items.join('/');
    this.loadChildren(fullPath);
  }

  goBack() {
    this.routeStack.pop();
    this.currentRoot = this.routeStack.peek();
    const fullPath = this.routeStack.items.join('/');
    this.loadChildren(fullPath);
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
    if (lastPath) {
      let replaced = path.replace(lastPath, '');
      if (replaced.startsWith('/') || replaced.startsWith('\\')) {
        replaced = replaced.substr(1, replaced.length);
      }
      return replaced;
    }

    return path;
  }

  navigateTo(index: number) {
    const numberOfPops = this.routeStack.items.length - index;
    if (this.routeStack.items.length - numberOfPops > this.routeStack.items.length) {
      this.routeStack.items = [];
    }
    for (let i = 0; i < numberOfPops; i++) {
      this.routeStack.pop();
    }

    this.loadChildren(this.routeStack.peek() || '');
  }
}
