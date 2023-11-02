export class Pagination {
    currentPage: number;
    itemsPerPage: number;
    totalItems: number;
    totalPages: number;

    constructor() {
      this.currentPage = 0;
      this.itemsPerPage = 0;
      this.totalItems = 0;
      this.totalPages = 0;
    }
}

export class PaginatedResult<T> {
    result!: T;
    pagination!: Pagination;
}
