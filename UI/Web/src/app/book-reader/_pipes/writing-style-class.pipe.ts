import {Pipe, PipeTransform} from '@angular/core';
import {WritingStyle} from "../../_models/preferences/writing-style";

@Pipe({
  name: 'writingStyleClass'
})
export class WritingStyleClassPipe implements PipeTransform {

  transform(value: WritingStyle): string {
    switch (value) {
      case WritingStyle.Horizontal:
        return '';
      case WritingStyle.Vertical:
        return 'writing-style-vertical';
    }
  }

}
