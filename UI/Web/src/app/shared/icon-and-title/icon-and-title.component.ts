import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

@Component({
  selector: 'app-icon-and-title',
  templateUrl: './icon-and-title.component.html',
  styleUrls: ['./icon-and-title.component.scss']
})
export class IconAndTitleComponent implements OnInit {
  /**
   * If the component is clickable and should emit click events
   */
  @Input() clickable: boolean = true;
  @Input() title: string = '';
  /**
   * Font classes used to display font
   */
  @Input() fontClasses: string = '';

  @Output() click: EventEmitter<MouseEvent> = new EventEmitter<MouseEvent>();



  constructor() { }

  ngOnInit(): void {
  }

  handleClick(event: MouseEvent) {
    if (this.clickable) this.click.emit(event);
  }

}
