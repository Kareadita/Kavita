import { Component, Input, OnInit } from '@angular/core';
import { MangaFormat } from 'src/app/_models/manga-format';
import { UtilityService } from '../_services/utility.service';

@Component({
  selector: 'app-series-format',
  templateUrl: './series-format.component.html',
  styleUrls: ['./series-format.component.scss']
})
export class SeriesFormatComponent implements OnInit {

  @Input() format: MangaFormat = MangaFormat.UNKNOWN;

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(public utilityService: UtilityService) { }

  ngOnInit(): void {
  }

}
