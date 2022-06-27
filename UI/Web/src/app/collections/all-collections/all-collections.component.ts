import { Component, EventEmitter, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { EditCollectionTagsComponent } from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';


@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss']
})
export class AllCollectionsComponent implements OnInit {

  isLoading: boolean = true;
  collections: CollectionTag[] = [];
  collectionTagActions: ActionItem<CollectionTag>[] = [];
  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();

  constructor(private collectionService: CollectionTagService, private router: Router,
    private actionFactoryService: ActionFactoryService, private modalService: NgbModal, 
    private titleService: Title, private imageService: ImageService) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.titleService.setTitle('Kavita - Collections');
  }

  ngOnInit() {
    this.loadPage();
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));
  }


  loadCollection(item: CollectionTag) {
    this.router.navigate(['collections', item.id]);
    this.loadPage();
  }

  loadPage() {
    this.collectionService.allTags().subscribe(tags => {
      this.collections = tags;
      this.isLoading = false;

      const keys: {[key: string]: number} = {};
      tags.forEach(s => {
        let ch = s.title.charAt(0);
        if (/\d|\#|!|%|@|\(|\)|\^|\*/g.test(ch)) {
          ch = '#';
        }
        if (!keys.hasOwnProperty(ch)) {
          keys[ch] = 0;
        }
        keys[ch] += 1;
      });
      this.jumpbarKeys = Object.keys(keys).map(k => {
        return {
          key: k,
          size: keys[k],
          title: k.toUpperCase()
        }
      }).sort((a, b) => {
        if (a.key < b.key) return -1;
        if (a.key > b.key) return 1;
        return 0;
      });
    });
    
  }

  handleCollectionActionCallback(action: Action, collectionTag: CollectionTag) {
    switch (action) {
      case(Action.Edit):
        const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
        modalRef.componentInstance.tag = collectionTag;
        modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
          this.loadPage();
          if (results.coverImageUpdated) {
            collectionTag.coverImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(collectionTag.id));
          }
        });
        break;
      default:
        break;
    }
  }
}
