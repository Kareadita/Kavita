import {describe, expect, test} from '@jest/globals';
import { Renderer2, RendererFactory2 } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ReaderService } from 'src/app/_services/reader.service';
import { ManagaReaderService } from './managa-reader.service';


describe('MangaReaderService', () => {
    let service: ManagaReaderService;
    let renderer: Renderer2;
    let readerService: ReaderService;
  
    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [
          ManagaReaderService,
          { provide: Renderer2, useValue: {} },
          { provide: RendererFactory2, useValue: {} },
          { provide: ReaderService, useValue: {} }
        ]
      });
      service = TestBed.inject(ManagaReaderService);
      renderer = TestBed.inject(Renderer2);
      readerService = TestBed.inject(ReaderService);
    });
  
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

  describe('loadPageDimensions', () => {
    it('should map the dimensions of each page to their respective page numbers', () => {
      const dims = [
        { pageNumber: 1, width: 100, height: 200 },
        { pageNumber: 2, width: 200, height: 100 },
        { pageNumber: 3, width: 300, height: 200 },
        { pageNumber: 4, width: 400, height: 300 }
      ];
      service.loadPageDimensions(dims);
      expect(service.pageDimensions).toEqual({
        1: { width: 100, height: 200, isWide: false },
        2: { width: 200, height: 100, isWide: true },
        3: { width: 300, height: 200, isWide: true },
        4: { width: 400, height: 300, isWide: true }
      });
    });
  
    it('should map single pages to their corresponding pairs', () => {
      const dims = [
        { pageNumber: 1, width: 100, height: 200 },
        { pageNumber: 2, width: 200, height: 100 },
        { pageNumber: 3, width: 300, height: 200 },
        { pageNumber: 4, width: 400, height: 300 }
      ];
      service.loadPageDimensions(dims);
      expect(service.pairs).toEqual({
        1: 0,
        2: 2,
        3: 2,
        4: 2
      });
    });
  });
});
