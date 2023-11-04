import { Routes } from '@angular/router';
import { PdfReaderComponent } from '../pdf-reader/_components/pdf-reader/pdf-reader.component';

export const routes: Routes = [
  {
      path: ':chapterId',
      component: PdfReaderComponent,
  }
];
