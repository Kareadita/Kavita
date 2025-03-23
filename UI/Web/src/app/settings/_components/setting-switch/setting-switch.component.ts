import {
  AfterContentInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild, ElementRef,
  inject,
  Input,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
    selector: 'app-setting-switch',
    imports: [
        NgTemplateOutlet,
        TranslocoDirective,
        SafeHtmlPipe
    ],
    templateUrl: './setting-switch.component.html',
    styleUrl: './setting-switch.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingSwitchComponent implements AfterContentInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly elementRef = inject(ElementRef);

  @Input({required:true}) title: string = '';
  @Input() subtitle: string | undefined = undefined;
  @Input() id: string | undefined = undefined;
  @ContentChild('switch') switchRef!: TemplateRef<any>;

  /**
   * For wiring up with a real label
   */
  labelId: string = '';

  ngAfterContentInit(): void {
    setTimeout(() => {
      if (this.id) {
        this.labelId = this.id;
        this.cdRef.markForCheck();
        return;
      }

      const element = this.elementRef.nativeElement;
      const inputElement = element.querySelector('input');

      if (inputElement && inputElement.id) {
        this.labelId = inputElement.id;
        this.cdRef.markForCheck();
      } else {
        console.warn('No input with ID found in app-setting-switch. For accessibility, please ensure the input has an ID.');
      }
    });
  }

}
