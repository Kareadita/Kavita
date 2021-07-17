import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { EditCollectionTagsComponent } from '../_modals/edit-collection-tags/edit-collection-tags.component';
import { CollectionTag } from '../_models/collection-tag';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { CollectionTagService } from '../_services/collection-tag.service';
import { SeriesService } from '../_services/series.service';

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

  constructor(private collectionService: CollectionTagService, private router: Router, private route: ActivatedRoute, private seriesService: SeriesService, private toastr: ToastrService, private actionFactoryService: ActionFactoryService, private modalService: NgbModal) {
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
    // TODO: See if we can move this pagination code into layout code
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
        modalRef.closed.subscribe((reloadNeeded: boolean) => {
          if (reloadNeeded) {
            this.loadPage();
          }
        });
        break;
      default:
        break;
    }
  }

}
