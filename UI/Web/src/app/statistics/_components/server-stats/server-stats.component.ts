import { ChangeDetectionStrategy, Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, map, Observable, of, shareReplay, Subject, takeUntil, tap } from 'rxjs';
import { FilterQueryParam } from 'src/app/shared/_services/filter-utilities.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Series } from 'src/app/_models/series';
import { ImageService } from 'src/app/_services/image.service';
import { MetadataService } from 'src/app/_services/metadata.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { ServerStatistics } from '../../_models/server-statistics';
import { GenericListModalComponent } from '../_modals/generic-list-modal/generic-list-modal.component';

@Component({
  selector: 'app-server-stats',
  templateUrl: './server-stats.component.html',
  styleUrls: ['./server-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerStatsComponent implements OnInit, OnDestroy {

  releaseYears$!: Observable<Array<PieDataItem>>;
  mostActiveUsers$!: Observable<Array<PieDataItem>>;
  mostActiveLibrary$!: Observable<Array<PieDataItem>>;
  mostActiveSeries$!: Observable<Array<PieDataItem>>;
  recentlyRead$!: Observable<Array<PieDataItem>>;
  stats$!: Observable<ServerStatistics>;
  seriesImage: (data: PieDataItem) => string;
  private readonly onDestroy = new Subject<void>();
  openSeries = (data: PieDataItem) => {
    const series = data.extra as Series;
    this.router.navigate(['library', series.libraryId, 'series', series.id]);
  }

  breakpointSubject = new BehaviorSubject<Breakpoint>(1);
  breakpoint$: Observable<Breakpoint> = this.breakpointSubject.asObservable();

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize() {  
    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());
  }


  get Breakpoint() { return Breakpoint; }

  constructor(private statService: StatisticsService, private router: Router, private imageService: ImageService, 
    private metadataService: MetadataService, private modalService: NgbModal, private utilityService: UtilityService) {
    this.seriesImage = (data: PieDataItem) => {
      if (data.extra) return this.imageService.getSeriesCoverImage(data.extra.id);
      return '';      
    }

    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());

    this.stats$ = this.statService.getServerStatistics().pipe(takeUntil(this.onDestroy), shareReplay());
    this.releaseYears$ = this.statService.getTopYears().pipe(takeUntil(this.onDestroy));
    this.mostActiveUsers$ = this.stats$.pipe(
      map(d => d.mostActiveUsers),
      map(userCounts => userCounts.map(count => {
        return {name: count.value.username, value: count.count};
      })),
      takeUntil(this.onDestroy)
    );

    this.mostActiveLibrary$ = this.stats$.pipe(
      map(d => d.mostActiveLibraries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count};
      })),
      takeUntil(this.onDestroy)
    );

    this.mostActiveSeries$ = this.stats$.pipe(
      map(d => d.mostReadSeries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count, extra: count.value};
      })),
      takeUntil(this.onDestroy)
    );

    this.recentlyRead$ = this.stats$.pipe(
      map(d => d.recentlyRead),
      map(counts => counts.map(count => {
        return {name: count.name, value: -1, extra: count};
      })),
      takeUntil(this.onDestroy)
    );
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  openGenreList() {
    this.metadataService.getAllGenres().subscribe(genres => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = genres.map(t => t.title);
      ref.componentInstance.title = 'Genres';
      ref.componentInstance.clicked = (item: string) => {
        const params: any = {};
        params[FilterQueryParam.Genres] = item;
        params[FilterQueryParam.Page] = 1;
        this.router.navigate(['all-series'], {queryParams: params});
      };
    });
  }

  openTagList() {
    this.metadataService.getAllTags().subscribe(tags => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = tags.map(t => t.title);
      ref.componentInstance.title = 'Tags';
      ref.componentInstance.clicked = (item: string) => {
        const params: any = {};
        params[FilterQueryParam.Tags] = item;
        params[FilterQueryParam.Page] = 1;
        this.router.navigate(['all-series'], {queryParams: params});
      };
    });
  }

  openPeopleList() {
    this.metadataService.getAllPeople().subscribe(people => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = [...new Set(people.map(person => person.name))];
      ref.componentInstance.title = 'People';
    });
  }

  

}
