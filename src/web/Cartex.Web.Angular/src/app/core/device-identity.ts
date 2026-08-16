const DEVICE_ID = 'cartex.deviceId';

export function webDeviceId(): string {
  let value = localStorage.getItem(DEVICE_ID);
  if (value) return value;
  value = crypto.randomUUID().replaceAll('-', '');
  localStorage.setItem(DEVICE_ID, value);
  return value;
}

export function webDeviceName(): string {
  const platform = (navigator as Navigator & { userAgentData?: { platform?: string } }).userAgentData?.platform
    || navigator.platform
    || 'Web';
  return `Web · ${platform}`;
}

/// HTTP header values are ASCII. A pretty name with a middle dot, or a platform string carrying
/// any accented character, makes a strict parser reject the whole request with 400 before it ever
/// reaches the API — which is exactly how login broke: the body was fine, the header was not.
export function asciiHeader(value: string): string {
  return value.replace(/[^ -~]/g, '-').trim() || 'Web';
}
