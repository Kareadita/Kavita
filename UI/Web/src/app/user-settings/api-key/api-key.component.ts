import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  model,
  signal,
  ViewChild
} from '@angular/core';
import {Clipboard} from '@angular/cdk/clipboard';
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-api-key',
    templateUrl: './api-key.component.html',
    styleUrls: ['./api-key.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, SettingItemComponent]
})
export class ApiKeyComponent {

  private readonly clipboard = inject(Clipboard);
  private readonly cdRef = inject(ChangeDetectorRef);

  title = input.required<string>();
  key = input.required<string>();
  tooltipText = input<string | undefined>(undefined);
  hideData = model(true);

  isDataHidden = signal(this.hideData());

  value = computed(() => {
    const hide = this.hideData() && this.isDataHidden();
    const key = this.key();

    return hide ? '•'.repeat(key.length) : key;
  })


  @ViewChild('apiKey') inputElem!: ElementRef;

  async copy() {
    this.inputElem.nativeElement.select();
    this.clipboard.copy(this.inputElem.nativeElement.value);
    this.inputElem.nativeElement.setSelectionRange(0, 0);
    this.cdRef.markForCheck();
  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key().length);
      this.cdRef.markForCheck();
    }
  }

  toggleVisibility(forceState: boolean | null = null) {
    if (!this.hideData()) return;

    if (forceState == null) {
      this.isDataHidden.update(x => !x);
    } else {
      this.isDataHidden.set(!forceState);
    }
  }

}
