import {HttpInterceptorFn} from '@angular/common/http';
import {inject} from '@angular/core';
import {ClientInfoService} from "../_services/client-info.service";

/**
 * HTTP interceptor that adds client information to outgoing requests.
 * Attaches the X-Kavita-Client header with browser, device, and screen information.
 * Also attaches X-Device-Id for persistent device identification.
 */
export const clientInfoInterceptor: HttpInterceptorFn = (req, next) => {
  const clientInfoService = inject(ClientInfoService);

  // Add custom header with client info
  const modifiedReq = req.clone({
    setHeaders: {
      'X-Kavita-Client': clientInfoService.getClientInfoHeader(),
      'X-Device-Id': clientInfoService.getDeviceId()
    }
  });

  return next(modifiedReq);
};
