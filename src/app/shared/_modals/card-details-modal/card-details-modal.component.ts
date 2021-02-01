import { Component, Input, OnInit } from '@angular/core';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';

@Component({
  selector: 'app-card-details-modal',
  templateUrl: './card-details-modal.component.html',
  styleUrls: ['./card-details-modal.component.scss']
})
export class CardDetailsModalComponent implements OnInit {

  @Input() data!: Volume | Series;

  constructor() { }

  ngOnInit(): void {

  }

  isVolume(object: any): object is Volume {
    return !('originalName' in object);
  }

}
