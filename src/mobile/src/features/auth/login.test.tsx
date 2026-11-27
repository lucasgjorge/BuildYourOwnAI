import { QueryClient } from '@tanstack/react-query'
import { useFonts } from 'expo-font'
import * as SecureStore from 'expo-secure-store'
import { fireEvent, renderRouter, screen } from 'expo-router/testing-library'
import { StyleSheet } from 'react-native'
import { apiFetch } from '../../shared/api/client'
import { saveTokens } from '../../shared/session'
import { fonts } from '../../shared/theme'
import { fakeApi, tokens } from '../../test/api'

const openApp = () => renderRouter('./src/app')

async function submitLogin(password = 'Passw0rd!') {
  await fireEvent.changeText(await screen.findByLabelText('E-mail'), 'ana@test.local')
  await fireEvent.changeText(screen.getByLabelText('Senha'), password)
  await fireEvent.press(screen.getByRole('button', { name: 'Entrar' }))
}

const ok = { 'POST /api/auth/login': { status: 200, body: tokens } }

// C5
it('login stores tokens and opens organizations', async () => {
  const api = fakeApi({ ...ok, 'GET /api/organizations': { status: 200, body: [] } })
  await openApp()

  await submitLogin()

  expect(await screen.findByText('Organizações')).toBeTruthy()
  expect(JSON.parse(api.mock.calls[0][1]!.body as string)).toEqual({ email: 'ana@test.local', password: 'Passw0rd!' })
  expect(await SecureStore.getItemAsync('byoai.accessToken')).toBe('access-123')
  expect(await SecureStore.getItemAsync('byoai.refreshToken')).toBe('refresh-456')

  await apiFetch('/api/organizations')
  expect(api.mock.calls.at(-1)![1]!.headers).toMatchObject({ Authorization: 'Bearer access-123' })
})

// C6 (login path; the refresh path is pending)
it('tokens and password are never logged', async () => {
  const spies = (['log', 'info', 'warn', 'error', 'debug'] as const).map(level => jest.spyOn(console, level))
  fakeApi(ok)
  await openApp()

  await submitLogin()
  await screen.findByText('Organizações')

  const logged = spies.flatMap(spy => spy.mock.calls.flat().map(String)).join('\n')
  for (const secret of ['Passw0rd!', 'access-123', 'refresh-456']) expect(logged).not.toContain(secret)
})

// C7
it('login failure shows invalid credentials', async () => {
  const api = fakeApi({ 'POST /api/auth/login': { status: 401, body: { title: 'Unauthorized' } } })
  await openApp()

  await submitLogin('wrong')

  expect(await screen.findByText('E-mail ou senha inválidos.')).toBeTruthy()
  expect(api).toHaveBeenCalledTimes(1)
  expect(screen.getByLabelText('E-mail').props.value).toBe('ana@test.local')
  expect(await SecureStore.getItemAsync('byoai.accessToken')).toBeNull()
})

// C15
it('network failure shows server unreachable', async () => {
  fakeApi({ 'POST /api/auth/login': 'offline' })
  await openApp()

  await submitLogin()

  expect(await screen.findByText('Não foi possível falar com o servidor.')).toBeTruthy()
})

// C62 (login; the register form is pending)
it('auth forms show processing state', async () => {
  const api = fakeApi({ 'POST /api/auth/login': 'never' })
  await openApp()

  await submitLogin()

  const button = await screen.findByRole('button', { name: 'Processando...' })
  expect(button.props.accessibilityState).toMatchObject({ disabled: true })
  await fireEvent.press(button)
  expect(api).toHaveBeenCalledTimes(1)
})

// C10
it('app start follows the stored session', async () => {
  fakeApi({})
  const first = await openApp()
  expect(await screen.findByRole('button', { name: 'Entrar' })).toBeTruthy()
  await first.unmount()

  await saveTokens(tokens)
  await openApp()

  expect(await screen.findByText('Organizações')).toBeTruthy()
  expect(screen.queryByLabelText('Senha')).toBeNull()
})

// C14
it('logout clears tokens and cache', async () => {
  const clear = jest.spyOn(QueryClient.prototype, 'clear')
  fakeApi({})
  await saveTokens(tokens)
  await openApp()

  await fireEvent.press(await screen.findByRole('button', { name: 'Sair' }))

  expect(await screen.findByLabelText('Senha')).toBeTruthy()
  expect(clear).toHaveBeenCalled()
  expect(await SecureStore.getItemAsync('byoai.accessToken')).toBeNull()
  expect(await SecureStore.getItemAsync('byoai.refreshToken')).toBeNull()
})

// C16
it('missing api url shows setup screen', async () => {
  const api = fakeApi({})
  delete process.env.EXPO_PUBLIC_API_URL

  await openApp()

  expect(await screen.findByText(/Defina EXPO_PUBLIC_API_URL em src\/mobile\/\.env/)).toBeTruthy()
  expect(api).not.toHaveBeenCalled()
})

// C45 (titles and text; the mono label "trecho N" arrives with the chat)
it('screens use the web fonts', async () => {
  fakeApi({})
  await openApp()

  const title = await screen.findByRole('heading', { name: 'Entrar' })
  const text = screen.getByText('Não tem conta? Crie na versão web.')

  expect(Object.keys(jest.mocked(useFonts).mock.calls[0][0] as object)).toEqual(
    expect.arrayContaining([fonts.display, fonts.sans, fonts.mono]),
  )
  expect(StyleSheet.flatten(title.props.style).fontFamily).toBe(fonts.display)
  expect(StyleSheet.flatten(text.props.style).fontFamily).toBe(fonts.sans)
})
