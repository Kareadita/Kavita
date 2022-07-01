import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';

@Component({
  selector: 'app-icon-and-title',
  templateUrl: './icon-and-title.component.html',
  styleUrls: ['./icon-and-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.Default
})
export class IconAndTitleComponent implements OnChanges {
  /**
   * If the component is clickable and should emit click events
   */
  @Input() clickable: boolean = true;
  @Input() title: string = '';
  @Input() label: string = '';
  /**
   * Font classes used to display font
   */
  @Input() fontClasses: string = '';

  @Output() click: EventEmitter<MouseEvent> = new EventEmitter<MouseEvent>();

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  ngOnChanges(changes: SimpleChanges): void {
    this.cdRef.markForCheck();
  }

  handleClick(event: MouseEvent) {
    if (this.clickable) this.click.emit(event);
  }

}
