import { Component, OnInit } from '@angular/core';
import { take } from 'rxjs/operators';
import { InProgressChapter } from '../_models/in-progress-chapter';
import { Library } from '../_models/library';
import { Series } from '../_models/series';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { ImageService } from '../_services/image.service';
import { LibraryService } from '../_services/library.service';
import { SeriesService } from '../_services/series.service';
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

  recentlyAdded: Series[] = [];
  inProgress: Series[] = [];
  continueReading: InProgressChapter[] = [];

  constructor(public accountService: AccountService, private libraryService: LibraryService, private seriesService: SeriesService, private imageService: ImageService) { }

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

    this.seriesService.getRecentlyAdded().subscribe((series) => {
      series.forEach(s => s.coverImage = this.imageService.getSeriesCoverImage(s.id));
      this.recentlyAdded = series;
    });

    this.seriesService.getInProgress().subscribe((series) => {
      series.forEach(s => s.coverImage = this.imageService.getSeriesCoverImage(s.id));
      this.inProgress = series;
    });

    // this.seriesService.getContinueReading().subscribe((chapters) => {
    //   chapters.forEach(s => s.coverImage = this.imageService.getChapterCoverImage(s.id));
    //   this.continueReading = chapters;
    // });
  }

  reloadSeries() {
    this.seriesService.getRecentlyAdded().subscribe((series) => {
      series.forEach(s => s.coverImage = this.imageService.getSeriesCoverImage(s.id));
      this.recentlyAdded = series;
    });

    this.seriesService.getInProgress().subscribe((series) => {
      series.forEach(s => s.coverImage = this.imageService.getSeriesCoverImage(s.id));
      this.inProgress = series;
    });
  }

  handleSectionClick(sectionTitle: string) {
    // TODO: Implement this in future. For now, it is not supported
  }

}
