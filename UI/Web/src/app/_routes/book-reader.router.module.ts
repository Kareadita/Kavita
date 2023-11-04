import { Routes } from '@angular/router';
import { BookReaderComponent } from '../book-reader/_components/book-reader/book-reader.component';

export const routes: Routes = [
  {
      path: ':chapterId',
      component: BookReaderComponent,
  }
];

