import { Component, Input, OnChanges } from '@angular/core';

@Component({
  selector: 'app-read-more',
  templateUrl: './read-more.component.html',
  styleUrls: ['./read-more.component.scss']
})
export class ReadMoreComponent implements OnChanges {

  @Input() text!: string;
  @Input() maxLength: number = 250;
  currentText!: string;
  hideToggle: boolean = true;

  public isCollapsed: boolean = true;

  toggleView() {
      this.isCollapsed = !this.isCollapsed;
      this.determineView();
  }
  determineView() {
      if (!this.text || this.text.length <= this.maxLength) {
          this.currentText = this.text;
          this.isCollapsed = false;
          this.hideToggle = true;
          return;
      }
      this.hideToggle = false;
      if (this.isCollapsed === true) {
        this.currentText = this.text.substring(0, this.maxLength);
        this.currentText = this.currentText.substr(0, Math.min(this.currentText.length, this.currentText.lastIndexOf(' ')));
        this.currentText = this.currentText + '…';
      } else if (this.isCollapsed === false)  {
          this.currentText = this.text;
      }

  }
  ngOnChanges() {
      this.determineView();
  }
}
