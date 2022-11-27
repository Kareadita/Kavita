import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { map, Observable, Subject, takeUntil } from 'rxjs';
import { MemberService } from 'src/app/_services/member.service';
import { PieDataItem } from '../../_models/pie-data-item';

@Component({
  selector: 'app-top-reads-by-extension',
  templateUrl: './top-reads-by-extension.component.html',
  styleUrls: ['./top-reads-by-extension.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopReadsByExtensionComponent implements OnInit, OnDestroy {

  fakeData: Array<PieDataItem> = [{name: 'Accel World', value: 1, extra: 1}, {name: 'Mob Psycho 100', value: 1, extra: 3}];
  formGroup: FormGroup;
  memberNames$: Observable<string[]>;
  private readonly onDestroy = new Subject<void>();
  
  constructor(private memberService: MemberService) { 
    this.formGroup = new FormGroup({
      'member': new FormControl('All users', [])
    });
    this.memberNames$ = this.memberService.getMemberNames().pipe(
      map(names => {
        return ['All users', ...names];
      }),
      takeUntil(this.onDestroy)
    );
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
