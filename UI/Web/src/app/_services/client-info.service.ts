import {Injectable, signal} from '@angular/core';
import {VersionService} from "./version.service";
import {AuthenticationType} from "../_models/progress/reading-session";

export enum ClientDevicePlatform {
  Unknown = 0,
  Windows = 1,
  MacOs = 2,
  Ios = 3,
  Linux = 4,
  Android = 5
}

export enum ClientDeviceType {
  Unknown = 0,
  WebBrowser = 1,
  WebApp = 2,
  KoReader = 3,
  Panels = 4,
  Librera = 5,
  OpdsClient = 6
}

export interface ClientInfo {
  browser: string;
  browserVersion: string;
  platform: ClientDevicePlatform;
  deviceType: string;
  screenWidth: number;
  screenHeight: number;
  orientation: string;
  appVersion: string | null;

  // Backend-only properties
  userAgent: string;
  ipAddress: string;
  authType: AuthenticationType;
  clientType: ClientDeviceType; // Web App, OPDS Reader, KOReader, etc
}

@Injectable({
  providedIn: 'root'
})
export class ClientInfoService {

  private static readonly DEVICE_FINGERPRINT_KEY = 'kavita-fingerprint-id';

  private readonly clientInfo = signal<Partial<ClientInfo>>(this.detectClientInfo());
  private readonly deviceFingerprint = signal<string>(this.getOrCreateDeviceFingerprint());

  constructor() {
    // Update orientation and screen size on resize/rotation
    window.addEventListener('resize', () => {
      this.clientInfo.update(info => ({
        ...info,
        screenWidth: window.innerWidth,
        screenHeight: window.innerHeight,
        orientation: this.getOrientation()
      }));
    });
  }

  /**
   * Gets the persistent device ID for this browser.
   * Generated once and stored in localStorage.
   */
  getDeviceId(): string {
    return this.deviceFingerprint();
  }

  /**
   * Clears the device fingerprint
   */
  clearDeviceFingerprint(): void {
    localStorage.removeItem(ClientInfoService.DEVICE_FINGERPRINT_KEY);
    this.deviceFingerprint.set(this.generateDeviceFingerprint());
  }

  private getOrCreateDeviceFingerprint(): string {
    // Try to get existing device ID from localStorage
    let deviceId = localStorage.getItem(ClientInfoService.DEVICE_FINGERPRINT_KEY);

    if (!deviceId) {
      // Generate new UUID v4
      deviceId = this.generateDeviceFingerprint();
      localStorage.setItem(ClientInfoService.DEVICE_FINGERPRINT_KEY, deviceId);
    }

    return deviceId;
  }

  private generateDeviceFingerprint(): string {
    // Generate UUID v4
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  /**
   * Returns a compact header string with client info
   * Format: "web-app/1.2.3 (Chrome/120.0; Windows; Desktop; 1920x1080; landscape)"
   */
  getClientInfoHeader(): string {
    const info = this.clientInfo();
    return `web-app/${info.appVersion} (${info.browser}/${info.browserVersion}; ${info.platform}; ${info.deviceType}; ${info.screenWidth}x${info.screenHeight}; ${info.orientation})`;
  }

  private detectClientInfo(): Partial<ClientInfo> {
    const ua = navigator.userAgent;

    return {
      browser: this.detectBrowser(ua),
      browserVersion: this.detectBrowserVersion(ua),
      platform: ClientDevicePlatform.Unknown,
      deviceType: this.detectDeviceType(ua), // Not Needed
      screenWidth: window.innerWidth,
      screenHeight: window.innerHeight,
      orientation: this.getOrientation(),
      appVersion: this.getAppVersion()
    };
  }

  private detectBrowser(ua: string) {
    if (ua.includes('Edg/')) return 'Edge';
    if (ua.includes('Chrome/')) return 'Chrome';
    if (ua.includes('Safari/') && !ua.includes('Chrome')) return 'Safari';
    if (ua.includes('Firefox/')) return 'Firefox';
    if (ua.includes('Opera/') || ua.includes('OPR/')) return 'Opera';
    return 'Unknown';
  }

  private detectBrowserVersion(ua: string) {
    let match: RegExpMatchArray | null = null;
    // TODO: Move to the backend
    if (ua.includes('Edg/')) {
      match = ua.match(/Edg\/(\d+\.\d+)/);
    } else if (ua.includes('Chrome/')) {
      match = ua.match(/Chrome\/(\d+\.\d+)/);
    } else if (ua.includes('Safari/') && !ua.includes('Chrome')) {
      match = ua.match(/Version\/(\d+\.\d+)/);
    } else if (ua.includes('Firefox/')) {
      match = ua.match(/Firefox\/(\d+\.\d+)/);
    }

    return match ? match[1] : 'Unknown';
  }

  private detectDeviceType(ua: string): string {
    // Check for tablet first (more specific)
    if (ua.includes('iPad') || (ua.includes('Android') && !ua.includes('Mobile'))) {
      return 'Tablet';
    }

    // Then check for mobile
    if (ua.includes('Mobile') || ua.includes('iPhone') || ua.includes('Android')) {
      return 'Mobile';
    }

    // Default to desktop
    return 'Desktop';
  }

  private getOrientation() {
    return window.innerWidth > window.innerHeight ? 'landscape' : 'portrait';
  }

  private getAppVersion() {
    return localStorage.getItem(VersionService.SERVER_VERSION_KEY);
  }

}
