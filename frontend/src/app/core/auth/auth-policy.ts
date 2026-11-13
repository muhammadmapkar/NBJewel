// Role: Restricts API credentials and sign-in return paths to known local targets.
export function isApiUrl(requestUrl: string, apiBaseUrl: string): boolean {
  try {
    if (!/^https?:\/\//i.test(requestUrl)) return false;
    const base = new URL(apiBaseUrl);
    const request = new URL(requestUrl, base);
    const apiPath = `${base.pathname.replace(/\/$/, '')}/api/`;
    return request.origin === base.origin && request.pathname.startsWith(apiPath);
  } catch { return false; }
}

export function safeErpReturnUrl(value: string | null): string {
  if (!value || !/^\/(?:home|catalog|scheme-master|chit-master|change-financial-year|change-password|owner\/jewelry)(?:\/[^?#]*)?(?:[?#].*)?$/.test(value)
      || /[\\;%\r\n]/.test(value)) return '/home';
  return value;
}
