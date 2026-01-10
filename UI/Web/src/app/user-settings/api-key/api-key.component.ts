import {
  ChangeDetectionStrategy,
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
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-api-key',
  templateUrl: './api-key.component.html',
  styleUrls: ['./api-key.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, SettingItemComponent]
})
export class ApiKeyComponent {

  private readonly clipboard = inject(Clipboard);
  private readonly toastr = inject(ToastrService);

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
    this.clipboard.copy(this.key());
    this.toastr.success(translate('toasts.copied-to-clipboard'));
  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key().length);
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
