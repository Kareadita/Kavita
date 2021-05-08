import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Library } from 'src/app/_models/library';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { LibraryService } from 'src/app/_services/library.service';

// Represents a library type card.
@Component({
  selector: 'app-library-card',
  templateUrl: './library-card.component.html',
  styleUrls: ['./library-card.component.scss']
})
export class LibraryCardComponent implements OnInit, OnChanges {
  @Input() data!: Library;
  @Output() clicked = new EventEmitter<Library>();

  isAdmin = false;
  actions: ActionItem<Library>[] = [];
  icon = 'fa-book-open';

  constructor(private accountService: AccountService, private router: Router,
              private libraryService: LibraryService, private toastr: ToastrService,
              private actionFactoryService: ActionFactoryService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }
    });
  }

  ngOnInit(): void {
  }

  ngOnChanges(changes: any) {
    if (this.data) {
      if (this.data.type === 0 || this.data.type === 1) {
        this.icon = 'fa-book-open';
      } else {
        this.icon = 'fa-book';
      }

      this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    }
  }

  handleAction(action: Action, library: Library) {
    switch (action) {
      case(Action.ScanLibrary):
        this.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        this.refreshMetadata(library);
        break;
      default:
        break;
    }
  }

  scanLibrary(library: Library) {
    this.libraryService.scan(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
    });
  }

  refreshMetadata(library: Library) {
    this.libraryService.refreshMetadata(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
    });
  }

  performAction(action: ActionItem<Library>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.data);
    }
  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.data?.id]);
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

}
