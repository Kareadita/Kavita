import { DevicePlatform } from "./device-platform";

export interface Device {
    id: number;
    name: string;
    platform: DevicePlatform;
    emailAddress: string;
    lastUsed: string;
    lastUsedUtc: string;
}
