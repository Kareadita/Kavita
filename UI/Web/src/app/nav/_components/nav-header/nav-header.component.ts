import {DOCUMENT} from '@angular/common';
import {ChangeDetectionStrategy, Component, computed, inject} from '@angular/core';
import {DownloadQueueWidgetComponent} from '../download-queue-widget/download-queue-widget.component';
import {RouterLink, RouterLinkActive} from '@angular/router';
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import {EventsWidgetComponent} from '../events-widget/events-widget.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {SettingsTabId} from "../../../sidenav/preference-nav/preference-nav.component";
import {WikiLink} from "../../../_models/wiki";
import {NavLinkModalComponent} from "../nav-link-modal/nav-link-modal.component";
import {MetadataService} from "../../../_services/metadata.service";
import {ProfileIconComponent} from "../../../_single-module/profile-icon/profile-icon.component";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ModalService} from "../../../_services/modal.service";
import {AccountService} from "../../../_services/account.service";
import {NavService} from "../../../_services/nav.service";
import {ImageService} from "../../../_services/image.service";
import {SearchTypeaheadComponent} from "../search-typeahead/search-typeahead.component";

@Component({
  selector: 'app-nav-header',
  templateUrl: './nav-header.component.html',
  styleUrls: ['./nav-header.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, SearchTypeaheadComponent, ImageComponent,
    EventsWidgetComponent, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem,
    SentenceCasePipe, TranslocoDirective, ProfileIconComponent, DownloadQueueWidgetComponent]
})
export class NavHeaderComponent {

  protected readonly accountService = inject(AccountService);
  protected readonly navService = inject(NavService);
  protected readonly imageService = inject(ImageService);
  protected readonly breakpointService = inject(BreakpointService);
  protected readonly modalService = inject(ModalService);
  protected readonly metadataService = inject(MetadataService);
  private readonly document = inject(DOCUMENT);


  profileLink = computed(() => {
    return ['/profile', this.accountService.currentUser()?.id ?? ''];
  });

  currentUser = computed(() => {
    return this.accountService.currentUser();
  });
  moveFocus() {
    this.document.getElementById('content')?.focus();
  }


  toggleSideNav(event: any) {
    console.log('nav-header: toggling side nav');
    event.stopPropagation();
    this.navService.toggleSideNav();
  }

  openLinkSelectionMenu() {
    this.modalService.open(NavLinkModalComponent, {fullscreen: 'sm'});
  }

  protected readonly FilterField = SeriesFilterField;
  protected readonly WikiLink = WikiLink;
  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly SettingsTabId = SettingsTabId;
}
