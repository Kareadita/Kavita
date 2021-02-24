import { Component, OnInit } from '@angular/core';
import { take } from 'rxjs/operators';
import { Library } from '../_models/library';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { LibraryService } from '../_services/library.service';
@Component({
  selector: 'app-library',
  templateUrl: './library.component.html',
  styleUrls: ['./library.component.scss']
})
export class LibraryComponent implements OnInit {

  user: User | undefined;
  libraries: Library[] = [];
  isLoading = false;
  isAdmin = false;

  constructor(public accountService: AccountService, private libraryService: LibraryService) { }

  ngOnInit(): void {
    this.isLoading = true;
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      this.isAdmin = this.accountService.hasAdminRole(this.user);
      this.libraryService.getLibrariesForMember().subscribe(libraries => {
        this.libraries = libraries;
        this.isLoading = false;
      });
    });
  }

}
