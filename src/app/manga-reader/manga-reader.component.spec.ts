import { ActivatedRoute, Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { MemberService } from '../_services/member.service';
import { MangaReaderComponent } from './manga-reader.component';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NavService } from '../_services/nav.service';
import { ReaderService } from '../_services/reader.service';
import { AccountService } from '../_services/account.service';
import { SeriesService } from '../_services/series.service';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';

// describe('MangaReaderComponent', () => {
//   let accountServiceMock: any;
//   let routerMock: any;
//   let memberServiceMock: any;
//   let fixture: MangaReaderComponent;
//   const http = jest.fn();

//   beforeEach(async () => {
//     accountServiceMock = {
//         login: jest.fn()
//     };
//     memberServiceMock = {
//         adminExists: jest.fn().mockReturnValue(of({
//             success: true,
//             message: false,
//             token: ''
//         }))
//   };
//     routerMock = {
//         navigateByUrl: jest.fn()
//     };
//     activatedRouteMock = {

//     };
//     /*
//     private route: ActivatedRoute, private router: Router, private accountService: AccountService,
//               private seriesService: SeriesService, private readerService: ReaderService, private location: Location,
//               private formBuilder: FormBuilder, private navService: NavService
//     */
//     fixture = new MangaReaderComponent(activatedRouteMock, routerMock, accountServiceMock,
//       seriesServiceMock, readerServiceMock, locationMock, formBuilder, navServiceMock
//     );
//     fixture.ngOnInit();
//   });

// });


describe('MangaReaderComponent ', () => {
  let component: MangaReaderComponent ;
  let fixture: ComponentFixture<MangaReaderComponent>;

  const mockReaderService = {
    getBookmark: jest.fn(),
    getPage: jest.fn(),
    //bookmark: jest.spyOn('bookmark') // todo: spy
  };

  const mockAccountService = {
    currentUser$: of({})
  };

  const mockActivatedRoute = {
    navigateByUrl: jest.fn(),
    snapshot: {
      paramMap: {
        get: jest.fn()
      }
    }
  };

  const mockLocation = {
    back: jest.fn() // todo: spy
  };

  const mockSeriesService = {
    getChapter: jest.fn()
  };

  const mockNavService = {
    showNavBar: jest.fn()
  };

  beforeEach((async () => {
    TestBed.configureTestingModule({
        imports: [
            //ActivatedRoute,
            //Router,
            //Location,
            ReactiveFormsModule,
            FormsModule,
            //FormBuilder,
            //NavService,
            //ReaderService,
            //AccountService,
            //SeriesService,
            
            NgbModalModule
        ],
      declarations: [ MangaReaderComponent],
      providers: [
        {
          provide: ReaderService,
          useValue: mockReaderService
        },
        {
          provide: AccountService,
          useValue: mockAccountService
        },
        {
          provide: ActivatedRoute,
          useValue: mockActivatedRoute
        },
        {
          provide: Location,
          useValue: mockLocation
        },
        {
          provide: SeriesService,
          useValue: mockSeriesService
        },
        {
          provide: NavService,
          useValue: mockNavService
        },
      ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MangaReaderComponent );
    component = fixture.componentInstance;
    fixture.detectChanges();
  });
  it('should be created', () => {
    expect(component).toBeTruthy();
  });
});
