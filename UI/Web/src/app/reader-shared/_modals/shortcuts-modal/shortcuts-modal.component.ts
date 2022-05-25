import { Component, Input, OnInit } from '@angular/core';
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
})
export class ShortcutsModalComponent implements OnInit {
  @Input() shortcuts: Array<KeyboardShortcut> = [];

  constructor(public modal: NgbActiveModal) {}

  ngOnInit(): void {}

  close() {
    this.modal.close();
  }
}
