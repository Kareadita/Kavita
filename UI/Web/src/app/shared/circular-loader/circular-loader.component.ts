import { Component, ElementRef, Input, OnChanges, OnInit, Renderer2, ViewChild } from '@angular/core';

@Component({
  selector: 'app-circular-loader',
  templateUrl: './circular-loader.component.html',
  styleUrls: ['./circular-loader.component.scss']
})
export class CircularLoaderComponent implements OnInit {

  @Input() currentValue: number = 0;
  @Input() maxValue: number = 0;

  constructor(private renderer: Renderer2) { }

  ngOnInit(): void {
  }

}
