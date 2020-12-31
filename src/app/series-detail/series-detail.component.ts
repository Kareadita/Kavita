import { Component, OnInit } from '@angular/core';
import { Series } from '../_models/series';

@Component({
  selector: 'app-series-detail',
  templateUrl: './series-detail.component.html',
  styleUrls: ['./series-detail.component.scss']
})
export class SeriesDetailComponent implements OnInit {

  series: Series | undefined;

  constructor() { }

  ngOnInit(): void {
  }

}
