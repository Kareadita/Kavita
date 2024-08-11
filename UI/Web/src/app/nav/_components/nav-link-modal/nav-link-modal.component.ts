import {Component, inject, Input} from '@angular/core';
import {WikiLink} from "../../../_models/wiki";
import {NgbActiveModal, NgbDropdownItem} from "@ng-bootstrap/ng-bootstrap";
import {RouterLink} from "@angular/router";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";

@Component({
  selector: 'app-nav-link-modal',
  standalone: true,
  imports: [
    NgbDropdownItem,
    RouterLink,
    FilterPipe,
    ReactiveFormsModule,
    Select2Module,
    TranslocoDirective
  ],
  templateUrl: './nav-link-modal.component.html',
  styleUrl: './nav-link-modal.component.scss'
})
export class NavLinkModalComponent {

  @Input({required: true}) logoutFn!: () => void;

  private readonly modal = inject(NgbActiveModal);

  protected readonly WikiLink = WikiLink;

  close() {
    this.modal.close();
  }

  logout() {
    this.logoutFn();
  }

  protected readonly SettingsTabId = SettingsTabId;
}
