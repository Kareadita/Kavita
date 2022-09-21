import { DevicePlatform } from "./device-platform";

export interface Device {
    name: string;
    platform: DevicePlatform;
    emailAddress: string;
    lastUsed: string;
}