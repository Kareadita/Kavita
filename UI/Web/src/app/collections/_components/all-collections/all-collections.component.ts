import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnDestroy, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { map, of, Subject, takeUntil } from 'rxjs';
import { Observable } from 'rxjs/internal/Observable';
import { EditCollectionTagsComponent } from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Tag } from 'src/app/_models/tag';
import { AccountService } from 'src/app/_services/account.service';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";


@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AllCollectionsComponent implements OnInit {

  isLoading: boolean = true;
  collections: CollectionTag[] = [];
  collectionTagActions: ActionItem<CollectionTag>[] = [];
  jumpbarKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: CollectionTag) => `${item.id}_${item.title}`;
  isAdmin$: Observable<boolean> = of(false);


  filterOpen: EventEmitter<boolean> = new EventEmitter();

  constructor(private collectionService: CollectionTagService, private router: Router,
    private actionFactoryService: ActionFactoryService, private modalService: NgbModal,
    private titleService: Title, private jumpbarService: JumpbarService,
    private readonly cdRef: ChangeDetectorRef, public imageSerivce: ImageService,
    public accountService: AccountService) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - Collections');
  }

  ngOnInit() {
    this.loadPage();
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));
    this.cdRef.markForCheck();
    this.isAdmin$ = this.accountService.currentUser$.pipe(takeUntilDestroyed(), map(user => {
      if (!user) return false;
      return this.accountService.hasAdminRole(user);
    }));
  }

  loadCollection(item: CollectionTag) {
    this.router.navigate(['collections', item.id]);
    this.loadPage();
  }

  loadPage() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.collectionService.allTags().subscribe(tags => {
      this.collections = [...tags];
      this.isLoading = false;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(tags, (t: Tag) => t.title);
      this.cdRef.markForCheck();
    });
  }

  handleCollectionActionCallback(action: ActionItem<CollectionTag>, collectionTag: CollectionTag) {
    switch (action.action) {
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
}
