import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { take } from 'rxjs/operators';
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

  constructor(public accountService: AccountService, private libraryService: LibraryService, private router: Router) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      this.user = user;
      console.log('user: ', this.user);
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
