import { Component, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { TagBadgeCursor } from '../shared/tag-badge/tag-badge.component';
import { ThemeService } from '../theme.service';
import { MangaFormat } from '../_models/manga-format';
import { Person, PersonRole } from '../_models/person';
import { Series } from '../_models/series';
import { NavService } from '../_services/nav.service';

@Component({
  selector: 'app-theme-test',
  templateUrl: './theme-test.component.html',
  styleUrls: ['./theme-test.component.scss']
})
export class ThemeTestComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'General', fragment: ''},
    {title: 'Users', fragment: 'users'},
    {title: 'Libraries', fragment: 'libraries'},
    {title: 'System', fragment: 'system'},
    {title: 'Changelog', fragment: 'changelog'},
  ];
  active = this.tabs[0];

  people: Array<Person> = [
    {id: 1, name: 'Joe', role: PersonRole.Artist},
    {id: 2, name: 'Joe 2', role: PersonRole.Artist},
  ];

  seriesNotRead: Series = {
    id: 1,
    name: 'Test Series',
    pages: 0,
    pagesRead: 10,
    format: MangaFormat.ARCHIVE,
    libraryId: 1,
    coverImageLocked: false,
    created: '',
    latestReadDate: '',
    localizedName: '',
    originalName: '',
    sortName: '', 
    userRating: 0,
    userReview: '', 
    volumes: [],
    localizedNameLocked: false,
    nameLocked: false, 
    sortNameLocked: false
  }

  seriesWithProgress: Series = {
    id: 1,
    name: 'Test Series',
    pages: 5,
    pagesRead: 10,
    format: MangaFormat.ARCHIVE,
    libraryId: 1,
    coverImageLocked: false,
    created: '',
    latestReadDate: '',
    localizedName: '',
    originalName: '',
    sortName: '', 
    userRating: 0,
    userReview: '', 
    volumes: [],
    localizedNameLocked: false,
    nameLocked: false, 
    sortNameLocked: false
  }

  get TagBadgeCursor(): typeof TagBadgeCursor {
    return TagBadgeCursor;
  }

  constructor(public toastr: ToastrService, public navService: NavService, public themeService: ThemeService) { }

  ngOnInit(): void {
  }

}
