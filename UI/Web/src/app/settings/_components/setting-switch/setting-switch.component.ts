import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild,
  inject,
  Input,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-setting-switch',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslocoDirective,
    SafeHtmlPipe
  ],
  templateUrl: './setting-switch.component.html',
  styleUrl: './setting-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingSwitchComponent {
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required:true}) title: string = '';
  @Input() subtitle: string | undefined = undefined;
  @Input() id: string | undefined = undefined;
  @ContentChild('switch') switchRef!: TemplateRef<any>;

}
