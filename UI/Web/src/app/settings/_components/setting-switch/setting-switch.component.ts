import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  ElementRef,
  input,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {generateUniqueId} from "../../../_helpers/random";
import {wireSettingControl} from "../../../_helpers/setting-item";
import {NgTemplateOutlet} from "@angular/common";

/**
 * Provides the setting-item styling and accessibility (id generation) for switches
 */
@Component({
    selector: 'app-setting-switch',
  imports: [
    TranslocoDirective,
    SafeHtmlPipe,
    NgTemplateOutlet
  ],
    templateUrl: './setting-switch.component.html',
    styleUrl: './setting-switch.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingSwitchComponent {

  title = input.required<string>();
  subtitle = input<string | undefined>();
  switchRef = contentChild(TemplateRef);

  switchWrapper = viewChild<ElementRef<HTMLElement>>('switchWrapper');
  private readonly wrapperScope = computed(() =>
    this.switchWrapper()?.nativeElement ?? null
  );
  private readonly generatedId = signal<string>(generateUniqueId());
  /** A unique id to wire id up */
  readonly elementId = computed(() => {
    return this.generatedId();
  });

  protected readonly hasControl = wireSettingControl({
    scope: this.wrapperScope,
    elementId: this.elementId,
    label: this.title,
  }).hasControl;
}
