export function isHttpUrl(value: string): boolean {
  return value.startsWith('http://') || value.startsWith('https://')
}
