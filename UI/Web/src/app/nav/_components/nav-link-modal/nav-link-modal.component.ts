import {ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, inject, Input} from '@angular/core';
import {WikiLink} from "../../../_models/wiki";
import {NgbActiveModal, NgbDropdownItem} from "@ng-bootstrap/ng-bootstrap";
import {Router, RouterLink} from "@angular/router";
import {ReactiveFormsModule} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {NavService} from "../../../_services/nav.service";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-nav-link-modal',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    TranslocoDirective,
    NgbDropdownItem
  ],
  templateUrl: './nav-link-modal.component.html',
  styleUrl: './nav-link-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NavLinkModalComponent {

  protected readonly WikiLink = WikiLink;
  protected readonly SettingsTabId = SettingsTabId;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly modal = inject(NgbActiveModal);
  private readonly router = inject(Router);
  protected readonly navService = inject(NavService);
  private readonly accountService = inject(AccountService);

  profileLink = computed(() => {
    return `/profile/${this.accountService.currentUserSignal()?.id ?? ''}`
  })

  close() {
    this.modal.close();
  }

  closeIfOnSettings() {
    setTimeout(() => {
      const currentUrl =  this.router.url;
      if (currentUrl.startsWith('/settings')) {
        this.close();
      }
    }, 10);
  }
}
