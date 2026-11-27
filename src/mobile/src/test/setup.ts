// Device storage and fonts are native: tests replace them with in-memory stand-ins.
const mockStore = new Map<string, string>()

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (key: string) => mockStore.get(key) ?? null),
  setItemAsync: jest.fn(async (key: string, value: string) => void mockStore.set(key, value)),
  deleteItemAsync: jest.fn(async (key: string) => void mockStore.delete(key)),
}))

jest.mock('expo-font', () => ({
  ...jest.requireActual('expo-font'),
  useFonts: jest.fn(() => [true, null]),
}))

beforeEach(() => {
  mockStore.clear()
  process.env.EXPO_PUBLIC_API_URL = 'http://api.test'
})
