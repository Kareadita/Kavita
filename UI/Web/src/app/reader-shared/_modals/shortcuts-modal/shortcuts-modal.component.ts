import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {CommonModule} from "@angular/common";

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
  imports: [CommonModule, NgbModalModule],
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
