import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SideNavCompanionBarComponent } from './side-nav-companion-bar.component';

describe('SideNavCompanionBarComponent', () => {
  let component: SideNavCompanionBarComponent;
  let fixture: ComponentFixture<SideNavCompanionBarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SideNavCompanionBarComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(SideNavCompanionBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
