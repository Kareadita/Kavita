import { DevicePlatform } from "./device-platform";

export interface Device {
    name: string;
    devicePlatform: DevicePlatform;
    emailAddress: string;
}