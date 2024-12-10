import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {DecimalPipe} from "@angular/common";
import {Series} from "../_models/series";
import {Pagination} from "../_models/pagination";
import {JumpKey} from "../_models/jumpbar/jump-key";
import {ActivatedRoute, Router} from "@angular/router";
import {Title} from "@angular/platform-browser";
import {ActionFactoryService} from "../_services/action-factory.service";
import {ActionService} from "../_services/action.service";
import {MessageHubService} from "../_services/message-hub.service";
import {UtilityService} from "../shared/_services/utility.service";
import {PersonService} from "../_services/person.service";
import {BrowsePerson} from "../_models/person/browse-person";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {JumpbarService} from "../_services/jumpbar.service";
import {PersonCardComponent} from "../cards/person-card/person-card.component";
import {ImageService} from "../_services/image.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {CompactNumberPipe} from "../_pipes/compact-number.pipe";


@Component({
  selector: 'app-browse-authors',
  standalone: true,
  imports: [
    SideNavCompanionBarComponent,
    TranslocoDirective,
    CardDetailLayoutComponent,
    DecimalPipe,
    CardItemComponent,
    PersonCardComponent,
    CompactNumberPipe,
  ],
  templateUrl: './browse-authors.component.html',
  styleUrl: './browse-authors.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrowseAuthorsComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly titleService = inject(Title);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly hubService = inject(MessageHubService);
  private readonly utilityService = inject(UtilityService);
  private readonly personService = inject(PersonService);
  private readonly jumpbarService = inject(JumpbarService);
  protected readonly imageService = inject(ImageService);


  series: Series[] = [];
  isLoading = false;
  authors: Array<BrowsePerson> = [];
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: BrowsePerson) => `${item.id}`;

  ngOnInit() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.personService.getAuthorsToBrowse(undefined, undefined).subscribe(d => {
      this.authors = d.result;
      this.pagination = d.pagination;
      this.jumpKeys = this.jumpbarService.getJumpKeys(this.authors, d => d.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  goToPerson(person: BrowsePerson) {
    this.router.navigate(['person', person.name]);
  }

}
