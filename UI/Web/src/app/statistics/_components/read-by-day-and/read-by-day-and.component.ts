import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { LegendPosition } from '@swimlane/ngx-charts';
import { Observable, Subject, takeUntil } from 'rxjs';
import { MangaFormatPipe } from 'src/app/pipe/manga-format.pipe';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { LineDataItem } from '../../_models/line-data-item';

@Component({
  selector: 'app-read-by-day-and',
  templateUrl: './read-by-day-and.component.html',
  styleUrls: ['./read-by-day-and.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadByDayAndComponent implements OnInit, OnDestroy {

  view: [number, number] = [700, 400];
  gradient: boolean = true;
  showLegend: boolean = true;
  showLabels: boolean = true;
  isDoughnut: boolean = false;
  legendPosition: LegendPosition = LegendPosition.Right;
  colorScheme = {
    domain: ['#5AA454', '#A10A28', '#C7B42C', '#AAAAAA']
  };

  legend: boolean = true;
  animations: boolean = true;
  xAxis: boolean = true;
  yAxis: boolean = true;
  showYAxisLabel: boolean = true;
  showXAxisLabel: boolean = true;
  xAxisLabel: string = 'Time';
  yAxisLabel: string = 'Pages Read';
  timeline: boolean = true;

  data$!: Observable<Array<LineDataItem>>;
  groupedList: any = [];


  formControl: FormControl = new FormControl(true, []);
  private readonly onDestroy = new Subject<void>();

  constructor(private statService: StatisticsService) { }

  ngOnInit(): void {
    const options: Intl.DateTimeFormatOptions  = { month: "short", day: "numeric" };
    const mangaFormatPipe = new MangaFormatPipe();
    this.statService.getReadCountByDay().subscribe(data => {
      const gList = data.reduce((formats, entry) => {
        const formatTranslated = mangaFormatPipe.transform(entry.format);
        if (!formats[formatTranslated]) {
          formats[formatTranslated] = {
            name: formatTranslated,
            value: 0,
            series: []
          };
        }
        formats[formatTranslated].series.push({name: new Date(entry.value).toLocaleDateString("en-US", options), value: entry.count});

        return formats;
      }, {});

      console.log('gList: ', gList);
      this.groupedList = Object.keys(gList).map(format => {
        return {name: format, value: 0, series: gList[format].series}
      });
      console.log('grouped list: ', this.groupedList);
    });

  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
