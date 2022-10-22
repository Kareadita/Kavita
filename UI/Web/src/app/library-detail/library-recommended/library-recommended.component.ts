import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { filter, map, merge, Observable, shareReplay, Subject, takeUntil } from 'rxjs';
import { Genre } from 'src/app/_models/genre';
import { Series } from 'src/app/_models/series';
import { MetadataService } from 'src/app/_services/metadata.service';
import { RecommendationService } from 'src/app/_services/recommendation.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-library-recommended',
  templateUrl: './library-recommended.component.html',
  styleUrls: ['./library-recommended.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibraryRecommendedComponent implements OnInit, OnDestroy {

  @Input() libraryId: number = 0;

  quickReads$!: Observable<Series[]>;
  quickCatchups$!: Observable<Series[]>;
  highlyRated$!: Observable<Series[]>;
  onDeck$!: Observable<Series[]>;
  rediscover$!: Observable<Series[]>;
  moreIn$!: Observable<Series[]>;
  genre$!: Observable<Genre>;

  all$!: Observable<any>;

  private onDestroy: Subject<void> = new Subject();

  constructor(private recommendationService: RecommendationService, private seriesService: SeriesService, 
    private metadataService: MetadataService) { }

  ngOnInit(): void {

    this.quickReads$ = this.recommendationService.getQuickReads(this.libraryId, 0, 30)
                      .pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());

    this.quickCatchups$ = this.recommendationService.getQuickCatchupReads(this.libraryId, 0, 30)
                      .pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());

    this.highlyRated$ = this.recommendationService.getHighlyRated(this.libraryId, 0, 30)
                      .pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());
    
    this.rediscover$ = this.recommendationService.getRediscover(this.libraryId, 0, 30)
                      .pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());

    this.onDeck$ = this.seriesService.getOnDeck(this.libraryId, 0, 30)
                        .pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());

    this.genre$ = this.metadataService.getAllGenres([this.libraryId]).pipe(
                        takeUntil(this.onDestroy),
                        map(genres => genres[Math.floor(Math.random() * genres.length)]), 
                        shareReplay()
                  );
    this.genre$.subscribe(genre => {
      this.moreIn$ = this.recommendationService.getMoreIn(this.libraryId, genre.id, 0, 30).pipe(takeUntil(this.onDestroy), map(p => p.result), shareReplay());
    });

    this.all$ = merge(this.quickReads$, this.quickCatchups$, this.highlyRated$, this.rediscover$, this.onDeck$, this.genre$).pipe(takeUntil(this.onDestroy));
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }


  reloadInProgress(series: Series | number) {
    if (Number.isInteger(series)) {
      if (!series) {return;}
    }
    // If the update to Series doesn't affect the requirement to be in this stream, then ignore update request
    const seriesObj = (series as Series);
    if (seriesObj.pagesRead !== seriesObj.pages && seriesObj.pagesRead !== 0) {
      return;
    }
    
    this.quickReads$ = this.quickReads$.pipe(filter(series => !series.includes(seriesObj)));
    this.quickCatchups$ = this.quickCatchups$.pipe(filter(series => !series.includes(seriesObj)));
  }

}
