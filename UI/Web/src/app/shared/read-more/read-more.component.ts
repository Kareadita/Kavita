import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnChanges } from '@angular/core';

@Component({
  selector: 'app-read-more',
  templateUrl: './read-more.component.html',
  styleUrls: ['./read-more.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadMoreComponent implements OnChanges {
  /**
   * String to apply readmore on
   */
  @Input() text!: string;
  /**
   * Max length before apply read more. Defaults to 250 characters.
   */
  @Input() maxLength: number = 250;
  /**
   * If the field is collapsed and blur true, text will not be readable
   */
  @Input() blur: boolean = false;

  currentText!: string;
  hideToggle: boolean = true;
  isCollapsed: boolean = true;

  constructor(private readonly cdRef: ChangeDetectorRef) {}

  toggleView() {
    this.isCollapsed = !this.isCollapsed;
    this.determineView();
  }

  determineView() {
    if (!this.text || this.text.length <= this.maxLength) {
        this.currentText = this.text;
        this.isCollapsed = true;
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

    this.cdRef.markForCheck();
  }
  ngOnChanges() {
      this.determineView();
  }
}
