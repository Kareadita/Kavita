import {
  ChangeDetectionStrategy,
  Component,
  computed,
  EventEmitter,
  inject,
  OnInit,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {Router} from '@angular/router';
import {UserCollection} from 'src/app/_models/collection-tag';
import {Tag} from 'src/app/_models/tag';
import {AccountService} from 'src/app/_services/account.service';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {DecimalPipe} from '@angular/common';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {CollectionOwnerComponent} from "../collection-owner/collection-owner.component";
import {User} from "../../../_models/user/user";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {ActionService} from "../../../_services/action.service";
import {WikiLink} from "../../../_models/wiki";
import {EntityCardComponent} from "../../../cards/entity-card/entity-card.component";
import {CardEntity, CardEntityFactory, CollectionCardEntity} from "../../../_models/card/card-entity";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {ActionResult} from "../../../_models/actionables/action-result";


@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, CardDetailLayoutComponent, DecimalPipe,
    TranslocoDirective, CollectionOwnerComponent, BulkOperationsComponent, EntityCardComponent, PromotedIconComponent]
})
export class AllCollectionsComponent implements OnInit {

  private readonly collectionService = inject(CollectionTagService);
  private readonly router = inject(Router);
  private readonly jumpbarService = inject(JumpbarService);
  public readonly imageService = inject(ImageService);
  public readonly accountService = inject(AccountService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly actionService = inject(ActionService);
  private readonly cardConfigFactory = inject(CardConfigFactory);

  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly WikiLink = WikiLink;

  protected cardSubtitleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('subtitle');
  protected cardTitleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');


  isLoading = signal<boolean>(true);
  collections = signal<UserCollection[]>([]);
  collectionEntities = computed(() => this.collections().map(c => CardEntityFactory.collection(c)));

  collectionConfig = computed(() => {
    return this.cardConfigFactory.forCollection({shouldRenderAction: this.shouldRenderCollection.bind(this),
      titleRef: this.cardTitleTemplateRef(), metaTitleRef: this.cardSubtitleTemplateRef()});
  });

  jumpbarKeys = computed(() => this.jumpbarService.getJumpKeys(this.collections(), (t: Tag) => t.title));
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: CollectionCardEntity) => `${item.data.id}_${item.data.title}_${item.data.owner}_${item.data.promoted}`;


  constructor() {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;

    this.bulkSelectionService.registerDataSource('collection', () => this.collections());
    this.bulkSelectionService.registerPostAction((res: ActionResult<UserCollection[]>) => {
      if (res.effect === 'none') return;
      if (res.effect === 'update') {
        const updatedItems = this.collections().map(item => {
          const updated = res.entity.find(u => u.id === item.id);
          return updated ? { ...updated } : item;
        });
        this.collections.set([...updatedItems]);
        return;
      }
      this.loadPage();
    });
  }

  ngOnInit() {
    this.loadPage();
  }

  shouldRenderCollection(action: ActionItem<UserCollection>, entity: UserCollection, user: User) {

    const hasPromotionRights = this.accountService.canCurrentUserInvokeAction(action.action);

    switch (action.action) {
      case Action.Promote:
        return !hasPromotionRights || !entity.promoted;
      case Action.UnPromote:
        return !hasPromotionRights || entity.promoted;
      default:
        return true;
    }
  }

  updateCollection(updatedEntity: UserCollection) {
    console.log('updating collection: ', updatedEntity);
    const originalEntity = this.collections().find(s => s.id == updatedEntity.id);
    if (originalEntity) {
      Object.assign(originalEntity, updatedEntity);
      this.collections.set([...this.collections()]);
    }
  }

  loadPage() {
    this.isLoading.set(true);
    this.collectionService.allCollections().subscribe(tags => {
      this.collections.set([...tags]);
      this.isLoading.set(false);
    });
  }
}
