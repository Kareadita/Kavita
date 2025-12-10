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
import {AsyncPipe, DecimalPipe} from '@angular/common';
import {CardItemComponent} from '../../../cards/card-item/card-item.component';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {CollectionOwnerComponent} from "../collection-owner/collection-owner.component";
import {User} from "../../../_models/user/user";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {ActionService} from "../../../_services/action.service";
import {WikiLink} from "../../../_models/wiki";
import {DefaultModalOptions} from "../../../_models/default-modal-options";


@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, CardDetailLayoutComponent, CardItemComponent, AsyncPipe, DecimalPipe,
    TranslocoDirective, CollectionOwnerComponent, BulkOperationsComponent]
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
  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly actionService = inject(ActionService);

  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly WikiLink = WikiLink;

  isLoading: boolean = true;
  collections: UserCollection[] = [];
  collectionTagActions: ActionItem<UserCollection>[] = [];
  jumpbarKeys: Array<JumpKey> = [];
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: UserCollection) => `${item.id}_${item.title}_${item.owner}_${item.promoted}`;
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

    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (!user) return;
      this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(
        this.handleCollectionActionCallback.bind(this), this.shouldRenderCollection.bind(this))
        .filter(action => this.collectionService.actionListFilter(action, user));
      this.cdRef.markForCheck();
    });
  }

  shouldRenderCollection(action: ActionItem<UserCollection>, entity: UserCollection, user: User) {
    switch (action.action) {
      case Action.Promote:
        return !entity.promoted;
      case Action.UnPromote:
        return entity.promoted;
      default:
        return true;
    }
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
      case Action.Promote:
        this.collectionService.promoteMultipleCollections([collectionTag.id], true).subscribe(_ => this.loadPage());
        break;
      case Action.UnPromote:
        this.collectionService.promoteMultipleCollections([collectionTag.id], false).subscribe(_ => this.loadPage());
        break;
      case(Action.Delete):
        this.collectionService.deleteTag(collectionTag.id).subscribe(res => {
          this.loadPage();
          this.toastr.success(res);
        });
        break;
      case(Action.Edit):
        const modalRef = this.modalService.open(EditCollectionTagsComponent, DefaultModalOptions);
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

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedCollectionIndexies = this.bulkSelectionService.getSelectedCardsForSource('collection');
    const selectedCollections = this.collections.filter((col, index: number) => selectedCollectionIndexies.includes(index + ''));

    switch (action.action) {
      case Action.Promote:
        this.actionService.promoteMultipleCollections(selectedCollections, true, (success) => {
          if (!success) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.UnPromote:
        this.actionService.promoteMultipleCollections(selectedCollections, false, (success) => {
          if (!success) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleCollections(selectedCollections, (successful) => {
          if (!successful) return;
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }
}
