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
