import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CarouselReelComponent } from './carousel-reel.component';

describe('CarouselReelComponent', () => {
  let component: CarouselReelComponent;
  let fixture: ComponentFixture<CarouselReelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ CarouselReelComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(CarouselReelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
