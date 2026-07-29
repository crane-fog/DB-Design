import { isMockPersistenceEnabled } from '@/config/mock'

export const MOCK_STORAGE_NAMESPACE = 'db-design:mock:'
export const MOCK_SCHEMA_VERSION = 1
export const MOCK_DATA_KEY = `${MOCK_STORAGE_NAMESPACE}data`

interface StoredValue<TValue> {
  schemaVersion: number
  data: TValue
}

const memoryValues = new Map<string, unknown>()
const memoryCounters = new Map<string, number>()
const resetHandlers = new Set<() => void>()

function clone<TValue>(value: TValue): TValue {
  return structuredClone(value)
}

function getLocalStorage(): Storage | undefined {
  if (!isMockPersistenceEnabled() || typeof globalThis.localStorage === 'undefined') {
    return undefined
  }
  try {
    const probeKey = `${MOCK_STORAGE_NAMESPACE}probe`
    globalThis.localStorage.setItem(probeKey, '1')
    globalThis.localStorage.removeItem(probeKey)
    return globalThis.localStorage
  } catch {
    return undefined
  }
}

function readStoredValue<TValue>(key: string): StoredValue<TValue> | undefined {
  const storage = getLocalStorage()
  if (!storage) {
    const value = memoryValues.get(key)
    if (value) {
      return clone(value as StoredValue<TValue>)
    }
    return undefined
  }

  const raw = storage.getItem(key)
  if (!raw) {
    return undefined
  }
  try {
    const value = JSON.parse(raw) as StoredValue<TValue>
    if (!value || typeof value !== 'object' || typeof value.schemaVersion !== 'number') {
      throw new Error('invalid mock storage envelope')
    }
    return value
  } catch {
    storage.removeItem(key)
    return undefined
  }
}

function writeStoredValue<TValue>(key: string, value: StoredValue<TValue>) {
  const safeValue = clone(value)
  const storage = getLocalStorage()
  if (!storage) {
    memoryValues.set(key, safeValue)
    return
  }
  try {
    storage.setItem(key, JSON.stringify(safeValue))
  } catch {
    memoryValues.set(key, safeValue)
  }
}

export interface MockStore<TValue> {
  read(): TValue
  write(value: TValue): TValue
  update(mutator: (draft: TValue) => TValue | void): TValue
  reset(): TValue
}

export interface PersistedMockAdapter<TState> {
  read<TResult>(operation: () => TResult): TResult
  write<TResult>(operation: () => TResult | Promise<TResult>): Promise<TResult>
  reset(): TState
}

export function createPersistedMockProxy<TTarget extends object, TState>(
  target: TTarget,
  adapter: PersistedMockAdapter<TState>,
  writeMethods: ReadonlySet<PropertyKey>,
): TTarget {
  return new Proxy(target, {
    get(currentTarget, property, receiver) {
      const value = Reflect.get(currentTarget, property, receiver)
      if (typeof value !== 'function') {
        return value
      }
      return (...argumentsList: unknown[]) => {
        const invoke = () => value.apply(currentTarget, argumentsList)
        if (writeMethods.has(property)) {
          return adapter.write(invoke)
        }
        return adapter.read(invoke)
      }
    },
  })
}

export function createPersistedMockAdapter<TState>(options: {
  key: string
  seedFactory: () => TState
  snapshot: () => TState
  restore: (state: TState) => void
}): PersistedMockAdapter<TState> {
  const store = createMockStore(options.key, options.seedFactory)

  function hydrate() {
    options.restore(store.read())
  }

  function reset() {
    const state = store.reset()
    options.restore(state)
    return state
  }

  resetHandlers.add(() => {
    reset()
  })

  return {
    read(operation) {
      hydrate()
      return operation()
    },
    reset,
    async write(operation) {
      hydrate()
      const result = await operation()
      store.write(options.snapshot())
      return result
    },
  }
}

export function createMockStore<TValue>(key: string, seedFactory: () => TValue): MockStore<TValue> {
  let storageKey = key
  if (!key.startsWith(MOCK_STORAGE_NAMESPACE)) {
    storageKey = `${MOCK_STORAGE_NAMESPACE}${key}`
  }

  function seed() {
    return clone(seedFactory())
  }

  function read() {
    const stored = readStoredValue<TValue>(storageKey)
    if (!stored || stored.schemaVersion !== MOCK_SCHEMA_VERSION) {
      const initial = seed()
      writeStoredValue(storageKey, { data: initial, schemaVersion: MOCK_SCHEMA_VERSION })
      return clone(initial)
    }
    return clone(stored.data)
  }

  function write(value: TValue) {
    const safeValue = clone(value)
    writeStoredValue(storageKey, { data: safeValue, schemaVersion: MOCK_SCHEMA_VERSION })
    return clone(safeValue)
  }

  return {
    read,
    reset() {
      return write(seed())
    },
    update(mutator) {
      const draft = read()
      const next = mutator(draft) ?? draft
      return write(next)
    },
    write,
  }
}

export function resetMockData() {
  memoryValues.clear()
  memoryCounters.clear()
  const storage = getLocalStorage()
  if (!storage) {
    resetHandlers.forEach((handler) => handler())
    return
  }
  const keys: string[] = []
  for (let index = 0; index < storage.length; index += 1) {
    const key = storage.key(index)
    if (key?.startsWith(MOCK_STORAGE_NAMESPACE)) {
      keys.push(key)
    }
  }
  keys.forEach((key) => storage.removeItem(key))
  resetHandlers.forEach((handler) => handler())
}

export function nextMockId(key: string, initialValue = 1) {
  const counterKey = `${MOCK_STORAGE_NAMESPACE}id:${key}`
  const storage = getLocalStorage()
  let nextId = memoryCounters.get(counterKey)
  if (nextId === undefined && storage) {
    const raw = storage.getItem(counterKey)
    if (raw) {
      try {
        const stored = JSON.parse(raw) as StoredValue<number>
        if (stored.schemaVersion === MOCK_SCHEMA_VERSION && Number.isInteger(stored.data)) {
          nextId = stored.data
        }
      } catch {
        storage.removeItem(counterKey)
      }
    }
  }
  nextId = nextId ?? initialValue
  const result = nextId
  const followingId = result + 1
  memoryCounters.set(counterKey, followingId)
  if (storage) {
    try {
      storage.setItem(
        counterKey,
        JSON.stringify({ data: followingId, schemaVersion: MOCK_SCHEMA_VERSION }),
      )
    } catch {
      // Memory remains the fallback when browser storage is unavailable.
    }
  }
  return result
}

export function mockDelay<TValue>(
  factory: () => TValue | Promise<TValue>,
  milliseconds = 120,
): Promise<TValue> {
  return new Promise((resolve, reject) => {
    globalThis.setTimeout(() => {
      Promise.resolve().then(factory).then(resolve).catch(reject)
    }, milliseconds)
  })
}
