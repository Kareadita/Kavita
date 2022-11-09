import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-search',
  templateUrl: './search.component.html',
  styleUrls: ['./search.component.scss']
})
export class SearchComponent implements OnInit {

  originalQueryString: string = '';

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,) {

  }

  ngOnInit(): void {
    const queryString = this.route.snapshot.queryParamMap.get('query');
    console.log('query: ', queryString)
    if (queryString === undefined || queryString === null) {
      //this.router.navigateByUrl('/libraries');
      return;
    }
    this.originalQueryString = queryString;
  }

}
