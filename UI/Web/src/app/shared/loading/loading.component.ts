import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-loading',
  templateUrl: './loading.component.html',
  styleUrls: ['./loading.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoadingComponent implements OnInit {

  @Input() loading: boolean = false;
  @Input() message: string = '';
  /**
   * Uses absolute positioning to ensure it loads over content
   */
  @Input() absolute: boolean = false;
  
  constructor(private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    console.log('absolute: ', this.absolute);
  }

}
