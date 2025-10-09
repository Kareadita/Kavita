export const isSafari = [
    'iPad Simulator',
    'iPhone Simulator',
    'iPod Simulator',
    'iPad',
    'iPhone',
    'iPod'
  ].includes(navigator.platform)
  // iPad on iOS 13 detection
  || (navigator.userAgent.includes("Mac") && "ontouchend" in document);

/**
 * Detects if the browser is Chromium-based (Chrome, Edge, Opera, etc.)
 */
export const isChromiumBased = (): boolean => {
  const userAgent = navigator.userAgent.toLowerCase();
  // Check for Chrome/Chromium indicators
  return (userAgent.includes('chrome') ||
      userAgent.includes('crios') || // Chrome on iOS
      userAgent.includes('chromium') ||
      userAgent.includes('edg/') || // Edge Chromium
      userAgent.includes('opr/') || // Opera
      userAgent.includes('samsungbrowser')) &&
    !userAgent.includes('firefox') &&
    !userAgent.includes('safari') ||
    (userAgent.includes('safari') && userAgent.includes('chrome')); // Chrome includes Safari in UA
};

/**
 * Detects if the device is mobile or tablet
 */
export const isMobileDevice = (): boolean => {
  // Check for touch capability and screen size
  const hasTouchScreen = 'ontouchstart' in window ||
    navigator.maxTouchPoints > 0 ||
    (window.matchMedia && window.matchMedia('(pointer: coarse)').matches);

  // Additional mobile UA checks
  const mobileUA = /android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini/i.test(
    navigator.userAgent.toLowerCase()
  );

  // Screen width check for tablets
  const isSmallScreen = window.innerWidth <= 1024;

  return hasTouchScreen && (mobileUA || isSmallScreen);
};

/**
 * Detects if running on a Chromium-based mobile browser
 */
export const isMobileChromium = (): boolean => {
  return isChromiumBased() && isMobileDevice();
};

/**
 * Gets the Chrome/Chromium version
 */
export const getChromiumVersion = (): Version | null => {
  const userAgent = navigator.userAgent;
  const matches = userAgent.match(/(?:Chrome|CriOS|Edg|OPR)\/(\d+)\.(\d+)\.(\d+)/);

  if (matches) {
    return new Version(
      parseInt(matches[1], 10),
      parseInt(matches[2], 10),
      parseInt(matches[3], 10)
    );
  }

  return null;
};

/**
 * Represents a Version for a browser
 */
export class Version {
  major: number;
  minor: number;
  patch: number;

  constructor(major: number, minor: number, patch: number) {
    this.major = major;
    this.minor = minor;
    this.patch = patch;
  }

  isLessThan(other: Version): boolean {
    if (this.major < other.major) return true;
    if (this.major > other.major) return false;
    if (this.minor < other.minor) return true;
    if (this.minor > other.minor) return false;
    return this.patch < other.patch;
  }

  isGreaterThan(other: Version): boolean {
    if (this.major > other.major) return true;
    if (this.major < other.major) return false;
    if (this.minor > other.minor) return true;
    if (this.minor < other.minor) return false;
    return this.patch > other.patch;
  }

  isEqualTo(other: Version): boolean {
    return (
      this.major === other.major &&
      this.minor === other.minor &&
      this.patch === other.patch
    );
  }
}

export const getIosVersion = () => {
  const match = navigator.userAgent.match(/OS (\d+)_(\d+)_?(\d+)?/);
  if (match) {
    const major = parseInt(match[1], 10);
    const minor = parseInt(match[2], 10);
    const patch = parseInt(match[3] || '0', 10);

    return new Version(major, minor, patch);
  }
  return null;
}
