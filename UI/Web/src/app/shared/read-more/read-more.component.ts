import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnChanges} from '@angular/core';
import {NgClass} from "@angular/common";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-read-more',
    imports: [SafeHtmlPipe, TranslocoDirective, NgClass],
    templateUrl: './read-more.component.html',
    styleUrls: ['./read-more.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadMoreComponent implements OnChanges {
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * String to apply read more on
   */
  @Input({required: true}) text!: string;
  /**
   * Max length before apply read more. Defaults to 250 characters.
   */
  @Input() maxLength: number = 250;
  /**
   * If the field is collapsed and blur true, text will not be readable
   */
  @Input() blur: boolean = false;
  /**
   * If the read more toggle is visible
   */
  @Input() showToggle: boolean = true;

  currentText!: string;
  hideToggle: boolean = true;
  isCollapsed: boolean = true;


  toggleView() {
    this.isCollapsed = !this.isCollapsed;
    this.determineView();
  }

  determineView() {
    const text = this.text ? this.text.replace(/\n/g, '<br>') : '';

    if (!this.text || this.text.length <= this.maxLength) {
        this.currentText = text;
        this.isCollapsed = true;
        this.hideToggle = true;
        this.cdRef.markForCheck();
        return;
    }

    this.hideToggle = false;
    if (this.isCollapsed) {
      this.currentText = text.substring(0, this.maxLength);

      // Find last natural breaking point: space for English, or a CJK character boundary
      const match = this.currentText.match(/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}]+$/u);
      const lastSpace = this.currentText.lastIndexOf(' ');

      if (lastSpace > 0) {
        this.currentText = this.currentText.substring(0, lastSpace); // Prefer space for English
      } else if (match) {
        this.currentText = this.currentText.substring(0, this.currentText.length - match[0].length); // Trim CJK
      }

      this.currentText = this.currentText + '…';
    } else if (!this.isCollapsed)  {
      this.currentText = text;
    }

    this.cdRef.markForCheck();
  }

  ngOnChanges() {
      this.determineView();
  }
}
