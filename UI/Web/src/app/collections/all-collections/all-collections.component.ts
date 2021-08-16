import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { EditCollectionTagsComponent } from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { ActionItem, ActionFactoryService, Action } from 'src/app/_services/action-factory.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { SeriesService } from 'src/app/_services/series.service';


/**
 * This component is used as a standard layout for any card detail. ie) series, in-progress, collections, etc.
 */
@Component({
  selector: 'app-all-collections',
  templateUrl: './all-collections.component.html',
  styleUrls: ['./all-collections.component.scss']
})
export class AllCollectionsComponent implements OnInit {

  isLoading: boolean = true;
  collections: CollectionTag[] = [];
  collectionTagId: number = 0; // 0 is not a valid id, if 0, we will load all tags
  collectionTagName: string = '';
  series: Array<Series> = [];
  seriesPagination!: Pagination;
  collectionTagActions: ActionItem<CollectionTag>[] = [];

  constructor(private collectionService: CollectionTagService, private router: Router, private route: ActivatedRoute, 
    private seriesService: SeriesService, private toastr: ToastrService, private actionFactoryService: ActionFactoryService, 
    private modalService: NgbModal, private titleService: Title, private imageService: ImageService) {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;

    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId != null) {
      this.collectionTagId = parseInt(routeId, 10);
      this.collectionService.allTags().subscribe(tags => {
        this.collections = tags;
        const matchingTags = this.collections.filter(t => t.id === this.collectionTagId);
        if (matchingTags.length === 0) {
          this.toastr.error('You don\'t have access to any libraries this tag belongs to or this tag is invalid');
          this.router.navigate(['collections']);
          return;
        }
        this.collectionTagName = tags.filter(item => item.id === this.collectionTagId)[0].title;
        this.titleService.setTitle('Kavita - ' + this.collectionTagName + ' Collection');
      });
    }
  }

  ngOnInit() {
    this.loadPage();
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));
  }


  loadCollection(item: CollectionTag) {
    this.collectionTagId = item.id;
    this.collectionTagName = item.title;
    this.router.navigate(['collections', this.collectionTagId]);
    this.loadPage();
  }

  onPageChange(pagination: Pagination) {
    this.router.navigate(['collections', this.collectionTagId], {replaceUrl: true, queryParamsHandling: 'merge', queryParams: {page: this.seriesPagination.currentPage} });
  }

  loadPage() {
    const page = this.route.snapshot.queryParamMap.get('page');
    if (page != null) {
      if (this.seriesPagination === undefined || this.seriesPagination === null) {
        this.seriesPagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
      }
      this.seriesPagination.currentPage = parseInt(page, 10);
    }
    // Reload page after a series is updated or first load
    if (this.collectionTagId === 0) {
      this.collectionService.allTags().subscribe(tags => {
        this.collections = tags;
        this.isLoading = false;
      });
    } else {
      this.seriesService.getSeriesForTag(this.collectionTagId, this.seriesPagination?.currentPage, this.seriesPagination?.itemsPerPage).subscribe(tags => {
        this.series = tags.result;
        this.seriesPagination = tags.pagination;
        this.isLoading = false;
        window.scrollTo(0, 0);
      });
    }
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
