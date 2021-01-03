import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
import { CardItemAction } from '../shared/card-item/card-item.component';
import { Library } from '../_models/library';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { LibraryService } from '../_services/library.service';
import { MemberService } from '../_services/member.service';

@Component({
  selector: 'app-library',
  templateUrl: './library.component.html',
  styleUrls: ['./library.component.scss']
})
export class LibraryComponent implements OnInit {

  user: User | undefined;
  libraries: Library[] = [];
  actions: CardItemAction[] = [];

  constructor(public accountService: AccountService, private libraryService: LibraryService, private router: Router) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      if (this.accountService.hasAdminRole(user)) {
        this.actions = [
          {title: 'Scan Library', callback: (data: Library) => {
            console.log('You tried to scan library: ' + data.name);
          }}
        ];
      }
      this.libraryService.getLibrariesForMember(this.user.username).subscribe(libraries => {
        this.libraries = libraries;
        console.log('Libraries: ', this.libraries);
      });
    });
  }

  handleNavigation(event: any, library: Library) {
    this.router.navigateByUrl('/library/' + library.id);
  }

}
