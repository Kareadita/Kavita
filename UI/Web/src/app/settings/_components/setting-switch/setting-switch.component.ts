import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild,
  EventEmitter,
  inject,
  Input,
  Output, TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-setting-switch',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslocoDirective
  ],
  templateUrl: './setting-switch.component.html',
  styleUrl: './setting-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingSwitchComponent {
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required:true}) title: string = '';
  @Input() subtitle: string | undefined = undefined;
  @ContentChild('switch') switchRef!: TemplateRef<any>;

}
