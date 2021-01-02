import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Library } from 'src/app/_models/library';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { LibraryService } from 'src/app/_services/library.service';
import { CardItemAction } from '../card-item/card-item.component';

// Represents a library type card. Uses a app-card-item internally
@Component({
  selector: 'app-library-card',
  templateUrl: './library-card.component.html',
  styleUrls: ['./library-card.component.scss']
})
export class LibraryCardComponent implements OnInit, OnChanges {
  @Input() data: Library | undefined;
  @Output() clicked = new EventEmitter<Library>();

  isAdmin = false;
  actions: CardItemAction[] = [];

  constructor(private accountService: AccountService, private router: Router,
              private libraryService: LibraryService, private toastr: ToastrService) {
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
      this.generateActions();
    }
  }

  generateActions() {
    this.actions = [];

    if (this.isAdmin) {
      this.actions.push({title: 'Scan Library', callback: (data: Library) => {
        console.log('You tried to scan library: ' + data.name);
        this.libraryService.scan(data?.id).subscribe((res: any) => {
          this.toastr.success('Scan started for ' + data.name);
        });
      }});
    }
  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.data?.id]);
  }

}
