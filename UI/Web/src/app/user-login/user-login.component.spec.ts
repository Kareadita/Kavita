import { of } from 'rxjs';
import { MemberService } from '../_services/member.service';
import { UserLoginComponent } from './user-login.component';

xdescribe('UserLoginComponent', () => {
  let accountServiceMock: any;
  let routerMock: any;
  let memberServiceMock: any;
  let fixture: UserLoginComponent;
  const http = jest.fn();

  beforeEach(async () => {
    accountServiceMock = {
        login: jest.fn()
    };
    memberServiceMock = {
        adminExists: jest.fn().mockReturnValue(of({
            success: true,
            message: false,
            token: ''
        }))
    };
    routerMock = {
        navigateByUrl: jest.fn()
    };
    //fixture = new UserLoginComponent(accountServiceMock, routerMock, memberServiceMock);
    //fixture.ngOnInit();
  });

  describe('Test: ngOnInit', () => {
    xit('should redirect to /home if no admin user', done => {
        const response = {
            success: true,
            message: false,
            token: ''
        }
        const httpMock = {
            get: jest.fn().mockReturnValue(of(response))
        };
        const serviceMock = new MemberService(httpMock as any);
        serviceMock.adminExists().subscribe((data: any) => {
            expect(httpMock.get).toBeDefined();
            expect(httpMock.get).toHaveBeenCalled();
            expect(routerMock.navigateByUrl).toHaveBeenCalledWith('/home');
            done();
        });
    });

    xit('should initialize login form', () => {
        const loginForm = {
            username: '',
            password: ''
        };
        expect(fixture.loginForm.value).toEqual(loginForm);
    });
  });

  xdescribe('Test: Login Form', () => {
    it('should invalidate the form', () => {
        fixture.loginForm.controls.username.setValue('');
        fixture.loginForm.controls.password.setValue('');
        expect(fixture.loginForm.valid).toBeFalsy();
    });

    it('should validate the form', () => {
        fixture.loginForm.controls.username.setValue('demo');
        fixture.loginForm.controls.password.setValue('Pa$$word!');
        expect(fixture.loginForm.valid).toBeTruthy();
    });
  });

  xdescribe('Test: Form Invalid', () => {
    it('should not call login', () => {
        fixture.loginForm.controls.username.setValue('');
        fixture.loginForm.controls.password.setValue('');
        fixture.login();
        expect(accountServiceMock.login).not.toHaveBeenCalled();
    });
  });

//   describe('Test: Form valid', () => {
//     it('should call login', () => {
//         fixture.loginForm.controls.username.setValue('demo');
//         fixture.loginForm.controls.password.setValue('Pa$$word!');
//         const spyLoginUser = jest.spyOn(accountServiceMock, 'login').mockReturnValue();
//         fixture.login();
//         expect(accountServiceMock.login).not.toHaveBeenCalled();
//         const spyRouterNavigate = jest.spyOn(routerMock, 'navigateByUrl').mockReturnValue();
//     });
//   });
  


});
