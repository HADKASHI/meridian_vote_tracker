/** Storage that may be missing or throw (private windows, blocked site data); failures read as empty. */
export function readStorage(storage: () => Storage, key: string): string | null {
  try {
    return storage().getItem(key);
  } catch {
    return null;
  }
}

export function writeStorage(storage: () => Storage, key: string, value: string): void {
  try {
    storage().setItem(key, value);
  } catch {
    // Not persisted; the choice still holds for this page.
  }
}

export function removeStorage(storage: () => Storage, key: string): void {
  try {
    storage().removeItem(key);
  } catch {
    // Nothing stored to remove.
  }
}
