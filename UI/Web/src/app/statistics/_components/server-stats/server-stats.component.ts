import {ChangeDetectionStrategy, Component, DestroyRef, HostListener, inject} from '@angular/core';
import {Router} from '@angular/router';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {map, Observable, ReplaySubject, shareReplay} from 'rxjs';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {Series} from 'src/app/_models/series';
import {ImageService} from 'src/app/_services/image.service';
import {MetadataService} from 'src/app/_services/metadata.service';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {ServerStatistics} from '../../_models/server-statistics';
import {GenericListModalComponent} from '../_modals/generic-list-modal/generic-list-modal.component';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TimeDurationPipe} from '../../../_pipes/time-duration.pipe';
import {CompactNumberPipe} from '../../../_pipes/compact-number.pipe';
import {DayBreakdownComponent} from '../day-breakdown/day-breakdown.component';
import {ReadingActivityComponent} from '../reading-activity/reading-activity.component';
import {PublicationStatusStatsComponent} from '../publication-status-stats/publication-status-stats.component';
import {FileBreakdownStatsComponent} from '../file-breakdown-stats/file-breakdown-stats.component';
import {TopReadersComponent} from '../top-readers/top-readers.component';
import {StatListComponent} from '../stat-list/stat-list.component';
import {IconAndTitleComponent} from '../../../shared/icon-and-title/icon-and-title.component';
import {AsyncPipe, DecimalPipe, NgIf} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {AccountService} from "../../../_services/account.service";

@Component({
    selector: 'app-server-stats',
    templateUrl: './server-stats.component.html',
    styleUrls: ['./server-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, IconAndTitleComponent, StatListComponent, TopReadersComponent, FileBreakdownStatsComponent,
    PublicationStatusStatsComponent, ReadingActivityComponent, DayBreakdownComponent, AsyncPipe, DecimalPipe,
    CompactNumberPipe, TimeDurationPipe, BytesPipe, TranslocoDirective]
})
export class ServerStatsComponent {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly accountService = inject(AccountService);

  releaseYears$!: Observable<Array<PieDataItem>>;
  mostActiveUsers$!: Observable<Array<PieDataItem>>;
  mostActiveLibrary$!: Observable<Array<PieDataItem>>;
  mostActiveSeries$!: Observable<Array<PieDataItem>>;
  recentlyRead$!: Observable<Array<PieDataItem>>;
  stats$!: Observable<ServerStatistics>;
  seriesImage: (data: PieDataItem) => string;
  openSeries = (data: PieDataItem) => {
    const series = data.extra as Series;
    this.router.navigate(['library', series.libraryId, 'series', series.id]);
  }

  breakpointSubject = new ReplaySubject<Breakpoint>(1);
  breakpoint$: Observable<Breakpoint> = this.breakpointSubject.asObservable();



  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize() {
    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());
  }

  get Breakpoint() { return Breakpoint; }

  constructor(private statService: StatisticsService, private router: Router, private imageService: ImageService,
    private metadataService: MetadataService, private modalService: NgbModal, private utilityService: UtilityService,
    private filterUtilityService: FilterUtilitiesService) {
    this.seriesImage = (data: PieDataItem) => {
      if (data.extra) return this.imageService.getSeriesCoverImage(data.extra.id);
      return '';
    }

    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());

    this.stats$ = this.statService.getServerStatistics().pipe(takeUntilDestroyed(this.destroyRef), shareReplay());
    this.releaseYears$ = this.statService.getTopYears().pipe(takeUntilDestroyed(this.destroyRef));
    this.mostActiveUsers$ = this.stats$.pipe(
      map(d => d.mostActiveUsers),
      map(userCounts => userCounts.map(count => {
        return {name: count.value.username, value: count.count};
      })),
      takeUntilDestroyed(this.destroyRef)
    );

    this.mostActiveLibrary$ = this.stats$.pipe(
      map(d => d.mostActiveLibraries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count};
      })),
      takeUntilDestroyed(this.destroyRef)
    );

    this.mostActiveSeries$ = this.stats$.pipe(
      map(d => d.mostReadSeries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count, extra: count.value};
      })),
      takeUntilDestroyed(this.destroyRef)
    );

    this.recentlyRead$ = this.stats$.pipe(
      map(d => d.recentlyRead),
      map(counts => counts.map(count => {
        return {name: count.name, value: -1, extra: count};
      })),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  openGenreList() {
    this.metadataService.getAllGenres().subscribe(genres => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = genres.map(t => t.title);
      ref.componentInstance.title = translate('server-stats.genres');
      ref.componentInstance.clicked = (item: string) => {
        this.filterUtilityService.applyFilter(['all-series'], FilterField.Genres, FilterComparison.Contains, genres.filter(g => g.title === item)[0].id + '').subscribe();
      };
    });
  }

  openTagList() {
    this.metadataService.getAllTags().subscribe(tags => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = tags.map(t => t.title);
      ref.componentInstance.title = translate('server-stats.tags');
      ref.componentInstance.clicked = (item: string) => {
        this.filterUtilityService.applyFilter(['all-series'], FilterField.Tags, FilterComparison.Contains, tags.filter(g => g.title === item)[0].id + '').subscribe();
      };
    });
  }

  openPeopleList() {
    this.metadataService.getAllPeople().subscribe(people => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = [...new Set(people.map(person => person.name))];
      ref.componentInstance.title = translate('server-stats.people');
    });
  }



}
