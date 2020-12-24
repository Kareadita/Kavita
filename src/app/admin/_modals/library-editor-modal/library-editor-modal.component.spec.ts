import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LibraryEditorModalComponent } from './library-editor-modal.component';

describe('LibraryEditorModalComponent', () => {
  let component: LibraryEditorModalComponent;
  let fixture: ComponentFixture<LibraryEditorModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ LibraryEditorModalComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(LibraryEditorModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
