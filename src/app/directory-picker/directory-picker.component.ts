import { Component, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject } from 'rxjs';
import { LibraryService } from '../_services/library.service';

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
    this.loadChildren(folderName);
  }

  goBack() {
    this.routeStack.pop();
    this.currentRoot = this.routeStack.peek();
    this.loadChildren(this.currentRoot);
  }

  loadChildren(path: string) {
    this.libraryService.listDirectories(path).subscribe(folders => {
      this.folders = folders;
    });
  }

  shareFolder(folderName: string, event: any) {
    console.log(`You selected ${folderName} as your folder to share!`);
    event.preventDefault();
    event.stopPropagation();

    this.modal.close({success: true, folderPath: folderName});
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
}
