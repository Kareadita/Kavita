import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

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
  templateUrl: './shortcuts-modal.component.html',
  styleUrls: ['./shortcuts-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShortcutsModalComponent {

  @Input() shortcuts: Array<KeyboardShortcut> = [];

  constructor(public modal: NgbActiveModal) { }

  close() {
    this.modal.close();
  }
}
