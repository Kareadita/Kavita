import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {Router} from '@angular/router';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {map, of} from 'rxjs';
import {Observable} from 'rxjs/internal/Observable';
import {EditCollectionTagsComponent} from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import {UserCollection} from 'src/app/_models/collection-tag';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Tag} from 'src/app/_models/tag';
import {AccountService} from 'src/app/_services/account.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe, DecimalPipe, NgIf} from '@angular/common';
import {CardItemComponent} from '../../../cards/card-item/card-item.component';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {ToastrService} from "ngx-toastr";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {ProviderNamePipe} from "../../../_pipes/provider-name.pipe";
import {CollectionOwnerComponent} from "../collection-owner/collection-owner.component";
import {User} from "../../../_models/user";


@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, CardDetailLayoutComponent, CardItemComponent, AsyncPipe, DecimalPipe, TranslocoDirective, ProviderImagePipe, ProviderNamePipe, CollectionOwnerComponent]
})
export class AllCollectionsComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly translocoService = inject(TranslocoService);
  private readonly toastr = inject(ToastrService);
  private readonly collectionService = inject(CollectionTagService);
  private readonly router = inject(Router);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly modalService = inject(NgbModal);
  private readonly titleService = inject(Title);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly imageService = inject(ImageService);
  public readonly accountService = inject(AccountService);

  isLoading: boolean = true;
  collections: UserCollection[] = [];
  collectionTagActions: ActionItem<UserCollection>[] = [];
  jumpbarKeys: Array<JumpKey> = [];
  isAdmin$: Observable<boolean> = of(false);
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: UserCollection) => `${item.id}_${item.title}`;
  user!: User;


  constructor() {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - ' + this.translocoService.translate('all-collections.title'));
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (user) {
        this.user = user;
        this.cdRef.markForCheck();
      }
    });
  }

  ngOnInit() {
    this.loadPage();
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));
    this.cdRef.markForCheck();
    this.isAdmin$ = this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), map(user => {
      if (!user) return false;
      return this.accountService.hasAdminRole(user);
    }));
  }

  loadCollection(item: UserCollection) {
    this.router.navigate(['collections', item.id]);
  }

  loadPage() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.collectionService.allCollections().subscribe(tags => {
      this.collections = [...tags];
      this.isLoading = false;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(tags, (t: Tag) => t.title);
      this.cdRef.markForCheck();
    });
  }

  handleCollectionActionCallback(action: ActionItem<UserCollection>, collectionTag: UserCollection) {

    if (collectionTag.owner != this.user.username) {
      this.toastr.error(translate('toasts.collection-not-owned'));
      return;
    }

    switch (action.action) {
      case(Action.Delete):
        this.collectionService.deleteTag(collectionTag.id).subscribe(res => {
          this.toastr.success(res);
        });
        break;
      case(Action.Edit):
        const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
        modalRef.componentInstance.tag = collectionTag;
        modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
          if (results.success) {
            this.loadPage();
          }
        });
        break;
      default:
        break;
    }
  }

  protected readonly ScrobbleProvider = ScrobbleProvider;
}
