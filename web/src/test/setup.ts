import '@testing-library/jest-dom/vitest'

// The runtime's built-in localStorage (Node's experimental global) is a partial
// implementation without clear(); give tests a complete in-memory Storage.
class MemoryStorage {
  private m = new Map<string, string>()
  getItem(k: string) { return this.m.has(k) ? this.m.get(k)! : null }
  setItem(k: string, v: string) { this.m.set(k, String(v)) }
  removeItem(k: string) { this.m.delete(k) }
  clear() { this.m.clear() }
  key(i: number) { return [...this.m.keys()][i] ?? null }
  get length() { return this.m.size }
}
Object.defineProperty(globalThis, 'localStorage', { value: new MemoryStorage(), writable: true })
