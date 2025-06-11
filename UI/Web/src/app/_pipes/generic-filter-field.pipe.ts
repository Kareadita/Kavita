import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'genericFilterField'
})
export class GenericFilterFieldPipe implements PipeTransform {

  transform(value: unknown, ...args: unknown[]): unknown {
    return null;
  }

}
