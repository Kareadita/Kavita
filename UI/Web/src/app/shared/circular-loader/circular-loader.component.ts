import { Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-circular-loader',
  templateUrl: './circular-loader.component.html',
  styleUrls: ['./circular-loader.component.scss']
})
export class CircularLoaderComponent implements OnInit {

  @Input() currentValue: number = 0;
  @Input() maxValue: number = 0;
  @Input() animation: boolean = true;
  @Input() innerStrokeColor: string = 'transparent';

  constructor() { }

  ngOnInit(): void {
  }

}
