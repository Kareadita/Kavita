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

  constructor(public accountService: AccountService, private libraryService: LibraryService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      this.libraryService.getLibrariesForMember().subscribe(libraries => {
        this.libraries = libraries;
      });
    });
  }

}
