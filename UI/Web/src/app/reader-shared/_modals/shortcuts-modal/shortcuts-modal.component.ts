import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";

export interface KeyboardShortcut {
  /**
   * String representing key or key combo. Should use + for combos. Will render as upper case
   */
  key: string;
  /**
   * Description of how it works
   */
  description: string;
}

@Component({
  selector: 'app-shortcuts-modal',
  standalone: true,
  imports: [NgbModalModule, TranslocoDirective],
  templateUrl: './shortcuts-modal.component.html',
  styleUrls: ['./shortcuts-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShortcutsModalComponent {

  protected readonly modal = inject(NgbActiveModal);

  @Input() shortcuts: Array<KeyboardShortcut> = [];
}
